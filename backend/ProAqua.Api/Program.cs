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
    await DbSeeder.SeedAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
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
