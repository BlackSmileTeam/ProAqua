import { useEffect, useMemo, useState } from 'react'
import { NavLink, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import api, { setToken, getToken, clearToken } from './api'

function Login() {
  const navigate = useNavigate()
  const [phone, setPhone] = useState('+79000000001')
  const [password, setPassword] = useState('1234')
  const [error, setError] = useState('')

  async function onSubmit(e) {
    e.preventDefault()
    setError('')
    try {
      const { data } = await api.post('/api/auth/login', { phone, password })
      if (data.role !== 'Admin' && data.role !== 'Master') {
        setError('Доступ только для администратора или мастера')
        return
      }
      setToken(data.token)
      navigate('/')
    } catch (err) {
      const status = err.response?.status
      const msg = err.response?.data?.message
      if (!err.response || status === 502 || status === 503 || status === 504 || status === 500 && !msg) {
        setError('API недоступен. Запустите backend на порту 5080 (dotnet run).')
      } else {
        setError(msg || 'Ошибка входа')
      }
    }
  }

  return (
    <div className="login">
      <div className="panel">
        <h1 className="brand">Про<span>Аква</span> Admin</h1>
        <form onSubmit={onSubmit}>
          <div className="field">
            <label>Телефон</label>
            <input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+79..." />
          </div>
          <div className="field">
            <label>Пароль</label>
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>
          <button className="btn" type="submit">Войти</button>
        </form>
        {error && <p className="error">{error}</p>}
      </div>
    </div>
  )
}

function Shell({ children }) {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <h1 className="brand">Про<span>Аква</span></h1>
        <p>Админка мойки</p>
        <nav className="nav">
          <NavLink to="/" end>Обзор</NavLink>
          <NavLink to="/bookings">Записи</NavLink>
          <NavLink to="/services">Услуги</NavLink>
          <NavLink to="/clients">Клиенты</NavLink>
          <NavLink to="/staff">Сотрудники</NavLink>
        </nav>
        <button className="btn ghost" style={{ marginTop: 24 }} onClick={() => { clearToken(); window.location.href = '/login' }}>Выйти</button>
      </aside>
      <main className="content">{children}</main>
    </div>
  )
}

function Dashboard() {
  const [stats, setStats] = useState(null)
  useEffect(() => {
    api.get('/api/admin/analytics').then((r) => setStats(r.data)).catch(() => setStats(null))
  }, [])
  if (!stats) return <div className="panel">Загрузка аналитики…</div>
  return (
    <>
      <h2>Обзор за 30 дней</h2>
      <div className="grid">
        <div className="stat"><span>Записей</span><strong>{stats.bookingsTotal}</strong></div>
        <div className="stat"><span>Завершено</span><strong>{stats.completed}</strong></div>
        <div className="stat"><span>No-show %</span><strong>{stats.noShowRate}</strong></div>
        <div className="stat"><span>Средний LTV</span><strong>{stats.averageLtv} ₽</strong></div>
        <div className="stat"><span>Рефералы</span><strong>{stats.referralSignups}</strong></div>
        <div className="stat"><span>Конверсия рефералов %</span><strong>{stats.referralConversionPercent}</strong></div>
      </div>
      <div className="panel">
        <p className="hint">Клиентов регистрирует администратор при визите.</p>
      </div>
    </>
  )
}

function Bookings() {
  const [items, setItems] = useState([])
  const load = () => api.get('/api/admin/bookings').then((r) => setItems(r.data))
  useEffect(() => { load() }, [])

  async function setStatus(id, status) {
    await api.patch(`/api/admin/bookings/${id}/status`, { status })
    await load()
  }

  return (
    <div className="panel">
      <h2>Записи</h2>
      <table>
        <thead>
          <tr><th>Клиент</th><th>Услуга</th><th>Время</th><th>Статус</th><th></th></tr>
        </thead>
        <tbody>
          {items.map((b) => (
            <tr key={b.id}>
              <td>{b.client}</td>
              <td>{b.service}</td>
              <td>{new Date(b.startAt).toLocaleString('ru-RU')}</td>
              <td>{b.status}</td>
              <td>
                <button className="btn ghost" onClick={() => setStatus(b.id, 'InProgress')}>В работе</button>
                <button className="btn ghost" onClick={() => setStatus(b.id, 'Ready')}>Готово</button>
                <button className="btn" onClick={() => setStatus(b.id, 'Completed')}>Завершить</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function ServicesPage() {
  const [items, setItems] = useState([])
  useEffect(() => {
    api.get('/api/services').then((r) => setItems(r.data))
  }, [])
  return (
    <div className="panel">
      <h2>Каталог услуг</h2>
      <table>
        <thead>
          <tr><th>Название</th><th>Категория</th><th>Длительность</th><th>От, ₽</th></tr>
        </thead>
        <tbody>
          {items.map((s) => (
            <tr key={s.id}>
              <td>{s.title}</td>
              <td>{s.category}</td>
              <td>{s.durationMinutes} мин</td>
              <td>{s.priceFrom}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Clients() {
  const emptyForm = {
    phone: '+79',
    password: '',
    name: '',
    referralCode: '',
    vehicleBrand: '',
    vehicleModel: '',
    plateNumber: '',
    vehicleType: 0
  }
  const [items, setItems] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [msg, setMsg] = useState('')
  const [error, setError] = useState('')
  const [resetPassword, setResetPassword] = useState({})

  const load = () => api.get('/api/admin/clients').then((r) => setItems(r.data))
  useEffect(() => { load() }, [])

  function setField(key, value) {
    setForm((f) => ({ ...f, [key]: value }))
  }

  async function registerClient(e) {
    e.preventDefault()
    setMsg('')
    setError('')
    try {
      const { data } = await api.post('/api/admin/clients', form)
      setMsg(`${data.message}. Код: ${data.referralCode}`)
      setForm(emptyForm)
      await load()
    } catch (err) {
      setError(err.response?.data?.message || 'Не удалось зарегистрировать')
    }
  }

  async function doResetPassword(id) {
    const password = resetPassword[id]
    if (!password || password.length < 4) {
      setError('Пароль — минимум 4 символа')
      return
    }
    setError('')
    try {
      const { data } = await api.post(`/api/admin/clients/${id}/reset-password`, { password })
      setMsg(data.message)
      setResetPassword((s) => ({ ...s, [id]: '' }))
    } catch (err) {
      setError(err.response?.data?.message || 'Не удалось сбросить пароль')
    }
  }

  return (
    <>
      <div className="panel" style={{ marginBottom: 20 }}>
        <h2>Регистрация клиента</h2>
        <form onSubmit={registerClient}>
          <div className="grid" style={{ marginBottom: 0 }}>
            <div className="field"><label>Телефон *</label><input required value={form.phone} onChange={(e) => setField('phone', e.target.value)} /></div>
            <div className="field"><label>Пароль *</label><input required minLength={4} value={form.password} onChange={(e) => setField('password', e.target.value)} /></div>
            <div className="field"><label>Имя</label><input value={form.name} onChange={(e) => setField('name', e.target.value)} /></div>
            <div className="field"><label>Реф. код друга</label><input value={form.referralCode} onChange={(e) => setField('referralCode', e.target.value)} /></div>
            <div className="field"><label>Марка авто</label><input value={form.vehicleBrand} onChange={(e) => setField('vehicleBrand', e.target.value)} /></div>
            <div className="field"><label>Модель</label><input value={form.vehicleModel} onChange={(e) => setField('vehicleModel', e.target.value)} /></div>
            <div className="field"><label>Номер</label><input value={form.plateNumber} onChange={(e) => setField('plateNumber', e.target.value)} /></div>
            <div className="field">
              <label>Тип кузова</label>
              <select value={form.vehicleType} onChange={(e) => setField('vehicleType', Number(e.target.value))}>
                <option value={0}>Седан</option>
                <option value={1}>Кроссовер</option>
                <option value={2}>SUV</option>
                <option value={3}>Фургон</option>
              </select>
            </div>
          </div>
          <button className="btn" type="submit" style={{ marginTop: 12 }}>Зарегистрировать</button>
        </form>
        {msg && <p className="ok">{msg}</p>}
        {error && <p className="error">{error}</p>}
      </div>

      <div className="panel">
        <h2>Клиенты</h2>
        <table>
          <thead>
            <tr><th>Имя</th><th>Телефон</th><th>Баллы</th><th>Уровень</th><th>Реф. код</th><th>Пароль</th></tr>
          </thead>
          <tbody>
            {items.map((c) => (
              <tr key={c.id}>
                <td>{c.name}</td>
                <td>{c.phone}</td>
                <td>{c.loyaltyPoints}</td>
                <td>{c.loyaltyLevel}</td>
                <td>{c.referralCode}</td>
                <td>
                  <input
                    className="input-sm"
                    placeholder="новый"
                    value={resetPassword[c.id] || ''}
                    onChange={(e) => setResetPassword((s) => ({ ...s, [c.id]: e.target.value }))}
                  />
                  <button className="btn ghost" type="button" onClick={() => doResetPassword(c.id)}>OK</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function Staff() {
  const emptyForm = { phone: '+79', password: '', name: '', role: 'Master' }
  const [items, setItems] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [msg, setMsg] = useState('')
  const [error, setError] = useState('')
  const [resetPassword, setResetPassword] = useState({})

  const load = () => api.get('/api/admin/staff').then((r) => setItems(r.data)).catch((err) => {
    setError(err.response?.data?.message || 'Нет доступа (нужен Admin)')
  })
  useEffect(() => { load() }, [])

  async function createStaff(e) {
    e.preventDefault()
    setMsg('')
    setError('')
    try {
      const { data } = await api.post('/api/admin/staff', form)
      setMsg(data.message)
      setForm(emptyForm)
      await load()
    } catch (err) {
      setError(err.response?.data?.message || 'Не удалось создать')
    }
  }

  async function doResetPassword(id) {
    const password = resetPassword[id]
    if (!password || password.length < 4) {
      setError('Пароль — минимум 4 символа')
      return
    }
    try {
      const { data } = await api.post(`/api/admin/staff/${id}/reset-password`, { password })
      setMsg(data.message)
      setResetPassword((s) => ({ ...s, [id]: '' }))
    } catch (err) {
      setError(err.response?.data?.message || 'Не удалось сбросить пароль')
    }
  }

  return (
    <>
      <div className="panel" style={{ marginBottom: 20 }}>
        <h2>Новый сотрудник</h2>
        <form onSubmit={createStaff}>
          <div className="grid" style={{ marginBottom: 0 }}>
            <div className="field"><label>Телефон *</label><input required value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} /></div>
            <div className="field"><label>Пароль *</label><input required minLength={4} value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></div>
            <div className="field"><label>Имя</label><input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></div>
            <div className="field">
              <label>Роль *</label>
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
                <option value="Master">Мастер</option>
                <option value="Admin">Администратор</option>
              </select>
            </div>
          </div>
          <button className="btn" type="submit" style={{ marginTop: 12 }}>Добавить</button>
        </form>
        {msg && <p className="ok">{msg}</p>}
        {error && <p className="error">{error}</p>}
      </div>
      <div className="panel">
        <h2>Сотрудники</h2>
        <table>
          <thead>
            <tr><th>Имя</th><th>Телефон</th><th>Роль</th><th>Пароль</th></tr>
          </thead>
          <tbody>
            {items.map((s) => (
              <tr key={s.id}>
                <td>{s.name}</td>
                <td>{s.phone}</td>
                <td>{s.role}</td>
                <td>
                  <input
                    className="input-sm"
                    placeholder="новый"
                    value={resetPassword[s.id] || ''}
                    onChange={(e) => setResetPassword((x) => ({ ...x, [s.id]: e.target.value }))}
                  />
                  <button className="btn ghost" type="button" onClick={() => doResetPassword(s.id)}>OK</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function PrivateRoute({ children }) {
  const token = useMemo(() => getToken(), [])
  if (!token) return <Navigate to="/login" replace />
  return <Shell>{children}</Shell>
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<PrivateRoute><Dashboard /></PrivateRoute>} />
      <Route path="/bookings" element={<PrivateRoute><Bookings /></PrivateRoute>} />
      <Route path="/services" element={<PrivateRoute><ServicesPage /></PrivateRoute>} />
      <Route path="/clients" element={<PrivateRoute><Clients /></PrivateRoute>} />
      <Route path="/staff" element={<PrivateRoute><Staff /></PrivateRoute>} />
    </Routes>
  )
}
