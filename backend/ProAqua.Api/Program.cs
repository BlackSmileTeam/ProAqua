using System.Text;
using ProAqua.Api.Data;
using ProAqua.Api.Options;
using ProAqua.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.Configure<AmoCrmOptions>(builder.Configuration.GetSection(AmoCrmOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=127.0.0.1;Port=3306;Database=ProAqua;User=ProAqua;Password=ProAqua;SslMode=None;AllowPublicKeyRetrieval=True";

builder.Services.AddDbContext<ProAquaDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)), mySql =>
    {
        mySql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null);
    }));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient("amocrm");

var pushProvider = builder.Configuration.GetValue<string>("Push:Provider") ?? "Dev";
if (pushProvider.Equals("FcmHttpV1", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IPushSender, FcmHttpV1PushSender>();
else
    builder.Services.AddSingleton<IPushSender, DevPushSender>();

builder.Services.AddScoped<IAmoCrmSync, AmoCrmSyncService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BookingService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ПроАква API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProAquaDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsurePinHashColumnAsync(db);
    await EnsureMustChangePasswordColumnAsync(db);
    await EnsureAvatarUrlColumnAsync(db);
    await EnsurePromotionsTableAsync(db);
    await EnsureMediaBlobColumnsAsync(db);
    await DbSeeder.SeedAsync(db);
}

// Swagger UI enabled in Production by default (set Swagger:Enabled=false / ENABLE_SWAGGER=false to disable).
if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ПроАква API v1");
        c.RoutePrefix = "swagger";
    });
}
app.UseCors();
app.Use(async (ctx, next) =>
{
    var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Http");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    log.LogInformation("→ {Method} {Path}{Query} ip={Ip}",
        ctx.Request.Method,
        ctx.Request.Path,
        ctx.Request.QueryString,
        ctx.Connection.RemoteIpAddress);
    try
    {
        await next();
    }
    finally
    {
        sw.Stop();
        log.LogInformation("← {Method} {Path} {Status} {Ms}ms",
            ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
    }
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "ПроАква", brand = "ProAqua" }));

app.Run();

static async Task EnsurePinHashColumnAsync(ProAquaDbContext db)
{
    try
    {
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Users'
              AND COLUMN_NAME = 'PinHash'
            """;
        var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        if (!exists)
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE Users
                ADD COLUMN PinHash varchar(200) NOT NULL DEFAULT '' AFTER Name
                """);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EnsurePinHashColumn skipped: {ex.Message}");
    }

    try
    {
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS SmsOtps");
    }
    catch
    {
        // ignore
    }
}

static async Task EnsureMustChangePasswordColumnAsync(ProAquaDbContext db)
{
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Users'
              AND COLUMN_NAME = 'MustChangePassword'
            """;
        var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        if (!exists)
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE Users
                ADD COLUMN MustChangePassword tinyint(1) NOT NULL DEFAULT 1 AFTER PinHash
                """);
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE Users SET MustChangePassword = 0 WHERE Role IN (1, 2)
                """);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EnsureMustChangePasswordColumn skipped: {ex.Message}");
    }
}

static async Task EnsureAvatarUrlColumnAsync(ProAquaDbContext db)
{
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Users'
              AND COLUMN_NAME = 'AvatarUrl'
            """;
        var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        if (!exists)
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE Users
                ADD COLUMN AvatarUrl varchar(500) NULL AFTER Name
                """);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EnsureAvatarUrlColumn skipped: {ex.Message}");
    }
}

static async Task EnsurePromotionsTableAsync(ProAquaDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Promotions (
              Id char(36) NOT NULL,
              Title varchar(200) NOT NULL,
              Description longtext NOT NULL,
              StartsAt datetime(6) NOT NULL,
              EndsAt datetime(6) NOT NULL,
              IsActive tinyint(1) NOT NULL,
              ImageUrl varchar(500) NULL,
              ImageData longblob NULL,
              ImageContentType varchar(100) NULL,
              CreatedAt datetime(6) NOT NULL,
              PRIMARY KEY (Id),
              KEY IX_Promotions_EndsAt (EndsAt)
            ) CHARACTER SET utf8mb4
            """);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EnsurePromotionsTable skipped: {ex.Message}");
    }
}

static async Task EnsureMediaBlobColumnsAsync(ProAquaDbContext db)
{
    try
    {
        await EnsureColumnAsync(db, "Services", "ImageData", "longblob NULL");
        await EnsureColumnAsync(db, "Services", "ImageContentType", "varchar(100) NULL");
        await EnsureColumnAsync(db, "Services", "ParentId", "char(36) NULL");
        await EnsureColumnAsync(db, "Services", "PriceSedan", "decimal(10,2) NULL");
        await EnsureColumnAsync(db, "Services", "PriceCrossover", "decimal(10,2) NULL");
        await EnsureColumnAsync(db, "Services", "PriceSuv", "decimal(10,2) NULL");
        await EnsureColumnAsync(db, "Services", "PriceSuvXl", "decimal(10,2) NULL");
        await EnsureColumnAsync(db, "Services", "Purpose", "varchar(400) NULL");
        await EnsureColumnAsync(db, "Services", "DetailsHtml", "longtext NULL");
        await EnsureColumnAsync(db, "Promotions", "ImageData", "longblob NULL");
        await EnsureColumnAsync(db, "Promotions", "ImageContentType", "varchar(100) NULL");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EnsureMediaBlobColumns skipped: {ex.Message}");
    }
}

static async Task EnsureColumnAsync(ProAquaDbContext db, string table, string column, string definition)
{
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
        await db.Database.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @table
          AND COLUMN_NAME = @column
        """;
    var pTable = cmd.CreateParameter();
    pTable.ParameterName = "@table";
    pTable.Value = table;
    cmd.Parameters.Add(pTable);
    var pCol = cmd.CreateParameter();
    pCol.ParameterName = "@column";
    pCol.Value = column;
    cmd.Parameters.Add(pCol);
    var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    if (!exists)
        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}");
}
