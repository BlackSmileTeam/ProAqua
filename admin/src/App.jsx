import { useEffect, useMemo, useState } from 'react'
import { NavLink, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import api, { setToken, getToken, clearToken } from './api'
import Modal from './components/Modal'
import ImageUploadPreview from './components/ImageUploadPreview'
import PageToolbar from './components/PageToolbar'
import ApiBanner from './components/ApiBanner'
import { toLocalInput, apiError } from './utils/files'

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
      setError(apiError(err, 'Ошибка входа'))
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
          <NavLink to="/promotions">Акции</NavLink>
          <NavLink to="/clients">Клиенты</NavLink>
          <NavLink to="/staff">Сотрудники</NavLink>
        </nav>
        <button className="btn ghost" style={{ marginTop: 24 }} onClick={() => { clearToken(); window.location.href = '/login' }}>Выйти</button>
      </aside>
      <main className="content">
        <ApiBanner />
        {children}
      </main>
    </div>
  )
}

function Dashboard() {
  const [stats, setStats] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => {
    api.get('/api/admin/analytics')
      .then((r) => { setStats(r.data); setError('') })
      .catch((err) => { setStats(null); setError(apiError(err)) })
  }, [])
  if (error) return <div className="panel"><p className="error">{error}</p></div>
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
  const [error, setError] = useState('')
  const load = () => api.get('/api/admin/bookings')
    .then((r) => { setItems(r.data); setError('') })
    .catch((err) => setError(apiError(err)))
  useEffect(() => { load() }, [])

  async function setStatus(id, status) {
    await api.patch(`/api/admin/bookings/${id}/status`, { status })
    await load()
  }

  return (
    <div className="panel">
      <h2>Записи</h2>
      {error && <p className="error">{error}</p>}
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

const SERVICE_CATEGORIES = [
  ['wash', 'Детейлинг мойка'],
  ['exterior', 'Экстерьер'],
  ['interior', 'Интерьер'],
  ['other', 'Прочие услуги'],
  ['education', 'Обучение'],
  ['ppf', 'Пакеты PPF'],
]

const blockId = () => `${Date.now()}-${Math.random().toString(16).slice(2, 8)}`
const makeBlock = (type) => {
  if (type === 'heading') return { id: blockId(), type, title: 'Новый заголовок' }
  if (type === 'text') return { id: blockId(), type, text: 'Текст блока' }
  if (type === 'list') return { id: blockId(), type, items: ['Пункт 1', 'Пункт 2'] }
  if (type === 'table') return {
    id: blockId(),
    type,
    title: '',
    headers: ['Колонка 1', 'Колонка 2'],
    rows: [['Значение 1', 'Значение 2']]
  }
  return { id: blockId(), type: 'text', text: '' }
}

const blocksToHtml = (blocks) => blocks.map((b) => {
  if (b.type === 'heading') return `<h3>${b.title || ''}</h3>`
  if (b.type === 'text') return `<p>${(b.text || '').replace(/\n/g, '<br/>')}</p>`
  if (b.type === 'list') {
    const items = (b.items || []).filter(Boolean).map((x) => `<li>${x}</li>`).join('')
    return `<ul>${items}</ul>`
  }
  if (b.type === 'table') {
    const head = (b.headers || []).map((h) => `<th>${h}</th>`).join('')
    const rows = (b.rows || []).map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join('')}</tr>`).join('')
    return `${b.title ? `<h4>${b.title}</h4>` : ''}<table><tr>${head}</tr>${rows}</table>`
  }
  return ''
}).join('')

const sampleCourseBlocks = () => ([
  { id: blockId(), type: 'heading', title: '📊 О курсе' },
  { id: blockId(), type: 'text', text: 'Базовый курс детейлинга — это идеальный старт для тех, кто хочет освоить профессию с нуля. 15 дней интенсивного обучения, цена — 60 000 ₽.' },
  {
    id: blockId(),
    type: 'table',
    title: 'Параметры',
    headers: ['Параметр', 'Значение'],
    rows: [['Длительность', '15 дней'], ['Стоимость', '60 000 ₽'], ['Группа', 'До 5 человек']]
  },
  { id: blockId(), type: 'heading', title: '🎯 Вы научитесь' },
  {
    id: blockId(),
    type: 'list',
    items: [
      'Проводить профессиональную мойку и подготовку авто',
      'Выполнять полировку кузова любой сложности',
      'Проводить химчистку салона',
      'Наносить защитные покрытия'
    ]
  }
])

function ServicesPage() {
  const empty = {
    title: '',
    description: '',
    purpose: '',
    detailsHtml: '',
    category: 'wash',
    durationMinutes: 60,
    priceFrom: 1000,
    sortOrder: 10,
    isActive: true,
    imagePreview: '',
    imageBase64: '',
    imageContentType: 'image/jpeg'
  }
  const [items, setItems] = useState([])
  const [open, setOpen] = useState(false)
  const [editId, setEditId] = useState(null)
  const [form, setForm] = useState(empty)
  const [msg, setMsg] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [blocks, setBlocks] = useState([])

  const load = () => api.get('/api/services/all')
    .then((r) => { setItems(r.data); setError('') })
    .catch(() => api.get('/api/services').then((r) => { setItems(r.data); setError('') }))
    .catch((err) => setError(apiError(err)))
  useEffect(() => { load() }, [])

  function openCreate() {
    setEditId(null)
    setForm(empty)
    setBlocks([])
    setError('')
    setOpen(true)
  }

  function openEdit(s) {
    setEditId(s.id)
    setForm({
      title: s.title || '',
      description: s.description || '',
      purpose: s.purpose || '',
      detailsHtml: s.detailsHtml || '',
      category: s.category || 'wash',
      durationMinutes: s.durationMinutes ?? 60,
      priceFrom: s.priceFrom ?? 0,
      sortOrder: s.sortOrder ?? 10,
      isActive: s.isActive !== false,
      imagePreview: s.imageUrl || '',
      imageBase64: '',
      imageContentType: 'image/jpeg'
    })
    setBlocks([])
    setError('')
    setOpen(true)
  }

  function addBlock(type) {
    setBlocks((prev) => [...prev, makeBlock(type)])
  }

  function moveBlock(id, dir) {
    setBlocks((prev) => {
      const idx = prev.findIndex((b) => b.id === id)
      if (idx < 0) return prev
      const ni = idx + dir
      if (ni < 0 || ni >= prev.length) return prev
      const arr = [...prev]
      const [x] = arr.splice(idx, 1)
      arr.splice(ni, 0, x)
      return arr
    })
  }

  function updateBlock(id, patch) {
    setBlocks((prev) => prev.map((b) => (b.id === id ? { ...b, ...patch } : b)))
  }

  function removeBlock(id) {
    setBlocks((prev) => prev.filter((b) => b.id !== id))
  }

  function buildHtmlFromBlocks() {
    const html = blocksToHtml(blocks)
    setForm((f) => ({ ...f, detailsHtml: html }))
  }

  async function saveService(e) {
    e.preventDefault()
    setSaving(true)
    setMsg('')
    setError('')
    const payload = {
      title: form.title,
      description: form.description,
      purpose: form.purpose,
      detailsHtml: form.detailsHtml,
      category: form.category,
      durationMinutes: Number(form.durationMinutes),
      priceFrom: Number(form.priceFrom),
      sortOrder: Number(form.sortOrder),
      isActive: form.isActive,
      imageBase64: form.imageBase64 || null,
      imageContentType: form.imageBase64 ? form.imageContentType : null,
      imageUrl: null,
      beforeAfterImageUrl: null
    }
    try {
      if (editId) {
        await api.put(`/api/services/${editId}`, payload)
        setMsg('Услуга обновлена')
      } else {
        await api.post('/api/services', payload)
        setMsg('Услуга добавлена')
      }
      setOpen(false)
      await load()
    } catch (err) {
      setError(apiError(err, 'Не удалось сохранить услугу'))
    } finally {
      setSaving(false)
    }
  }

  async function removeService(id) {
    if (!window.confirm('Удалить услугу?')) return
    try {
      await api.delete(`/api/services/${id}`)
      setMsg('Услуга удалена')
      await load()
    } catch (err) {
      setError(apiError(err, 'Не удалось удалить'))
    }
  }

  return (
    <>
      <div className="panel">
        <PageToolbar title="Каталог услуг" subtitle="Управление услугами для мобильного приложения" onAdd={openCreate} addLabel="Добавить услугу" />
        {msg && <p className="ok">{msg}</p>}
        {error && !open && <p className="error">{error}</p>}
        <table>
          <thead>
            <tr><th></th><th>Название</th><th>Категория</th><th>Длительность</th><th>От, ₽</th><th></th></tr>
          </thead>
          <tbody>
            {items.length === 0 && (
              <tr><td colSpan={6} className="hint">Нет услуг — нажмите «+ Добавить услугу»</td></tr>
            )}
            {items.map((s) => (
              <tr key={s.id}>
                <td>{s.imageUrl ? <img src={s.imageUrl} alt="" className="table-thumb" /> : '—'}</td>
                <td>{s.title}</td>
                <td>{s.category}</td>
                <td>{s.durationMinutes} мин</td>
                <td>{s.priceFrom}</td>
                <td className="table-actions">
                  <button className="btn ghost" type="button" onClick={() => openEdit(s)}>Изменить</button>
                  <button className="btn ghost" type="button" onClick={() => removeService(s.id)}>Удалить</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal
        open={open}
        title={editId ? 'Редактирование услуги' : 'Новая услуга'}
        onClose={() => setOpen(false)}
        wide
        footer={(
          <>
            <button type="button" className="btn ghost" onClick={() => setOpen(false)}>Отмена</button>
            <button type="submit" form="service-form" className="btn" disabled={saving}>
              {saving ? 'Сохранение…' : editId ? 'Сохранить' : 'Добавить услугу'}
            </button>
          </>
        )}
      >
        <form id="service-form" onSubmit={saveService}>
          <div className="form-split">
            <div className="form-split__media">
              <ImageUploadPreview
                value={form.imagePreview}
                onChange={({ dataUrl, contentType }) => setForm((f) => ({
                  ...f,
                  imagePreview: dataUrl,
                  imageBase64: dataUrl,
                  imageContentType: contentType
                }))}
                placeholder="Фото услуги"
              />
            </div>
            <div className="form-split__fields">
              <div className="field"><label>Название *</label><input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} /></div>
              <div className="field"><label>Описание</label><textarea rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
              <div className="field"><label>Коротко: для чего услуга</label><input value={form.purpose} onChange={(e) => setForm({ ...form, purpose: e.target.value })} placeholder="Например: Для старта в профессии детейлера" /></div>
              <div className="field">
                <label>Визуальный конструктор блоков</label>
                <div className="builder-toolbar">
                  <button type="button" className="btn ghost" onClick={() => addBlock('heading')}>+ Заголовок</button>
                  <button type="button" className="btn ghost" onClick={() => addBlock('text')}>+ Текст</button>
                  <button type="button" className="btn ghost" onClick={() => addBlock('list')}>+ Список</button>
                  <button type="button" className="btn ghost" onClick={() => addBlock('table')}>+ Таблица</button>
                </div>
                {blocks.map((b, idx) => (
                  <div key={b.id} className="builder-block">
                    <div className="builder-block__head">
                      <strong>{idx + 1}. {b.type === 'heading' ? 'Заголовок' : b.type === 'text' ? 'Текст' : b.type === 'list' ? 'Список' : 'Таблица'}</strong>
                      <div>
                        <button type="button" className="btn ghost" onClick={() => moveBlock(b.id, -1)}>↑</button>
                        <button type="button" className="btn ghost" onClick={() => moveBlock(b.id, 1)}>↓</button>
                        <button type="button" className="btn ghost" onClick={() => removeBlock(b.id)}>Удалить</button>
                      </div>
                    </div>
                    {b.type === 'heading' && (
                      <input value={b.title || ''} onChange={(e) => updateBlock(b.id, { title: e.target.value })} />
                    )}
                    {b.type === 'text' && (
                      <textarea rows={3} value={b.text || ''} onChange={(e) => updateBlock(b.id, { text: e.target.value })} />
                    )}
                    {b.type === 'list' && (
                      <textarea rows={4} value={(b.items || []).join('\n')} onChange={(e) => updateBlock(b.id, { items: e.target.value.split('\n').map((x) => x.trim()).filter(Boolean) })} placeholder="Один пункт в строке" />
                    )}
                    {b.type === 'table' && (
                      <>
                        <input placeholder="Заголовок таблицы (необязательно)" value={b.title || ''} onChange={(e) => updateBlock(b.id, { title: e.target.value })} />
                        <input placeholder="Заголовки колонок через |, например: Модуль|Длительность" value={(b.headers || []).join('|')} onChange={(e) => updateBlock(b.id, { headers: e.target.value.split('|').map((x) => x.trim()).filter(Boolean) })} />
                        <textarea rows={4} placeholder="Строки таблицы: колонки через |, новая строка = новая запись" value={(b.rows || []).map((r) => r.join('|')).join('\n')} onChange={(e) => updateBlock(b.id, { rows: e.target.value.split('\n').map((line) => line.split('|').map((x) => x.trim())).filter((r) => r.some(Boolean)) })} />
                      </>
                    )}
                  </div>
                ))}
                <div className="builder-toolbar">
                  <button type="button" className="btn ghost" onClick={() => { const bs = sampleCourseBlocks(); setBlocks(bs); setForm((f) => ({ ...f, detailsHtml: blocksToHtml(bs) })) }}>Шаблон курса</button>
                  <button type="button" className="btn" onClick={buildHtmlFromBlocks}>Собрать HTML из блоков</button>
                </div>
              </div>
              <div className="field">
                <label>Детальное описание (HTML, можно править вручную)</label>
                <textarea rows={10} value={form.detailsHtml} onChange={(e) => setForm({ ...form, detailsHtml: e.target.value })} placeholder="<h3>📊 О курсе</h3><p>...</p><table>...</table>" />
              </div>
              {form.detailsHtml && (
                <div className="field">
                  <label>Предпросмотр</label>
                  <div className="html-preview" dangerouslySetInnerHTML={{ __html: form.detailsHtml }} />
                </div>
              )}
              <div className="field"><label>Категория</label>
                <select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>
                  {SERVICE_CATEGORIES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select>
              </div>
              <div className="form-row">
                <div className="field"><label>Длительность, мин</label><input type="number" min="10" required value={form.durationMinutes} onChange={(e) => setForm({ ...form, durationMinutes: e.target.value })} /></div>
                <div className="field"><label>Цена от, ₽</label><input type="number" min="0" required value={form.priceFrom} onChange={(e) => setForm({ ...form, priceFrom: e.target.value })} /></div>
              </div>
              <div className="form-row">
                <div className="field"><label>Порядок</label><input type="number" value={form.sortOrder} onChange={(e) => setForm({ ...form, sortOrder: e.target.value })} /></div>
                <div className="field"><label>Активна</label>
                  <select value={form.isActive ? '1' : '0'} onChange={(e) => setForm({ ...form, isActive: e.target.value === '1' })}>
                    <option value="1">Да</option>
                    <option value="0">Нет</option>
                  </select>
                </div>
              </div>
            </div>
          </div>
          {error && <p className="error">{error}</p>}
        </form>
      </Modal>
    </>
  )
}

function PromotionsPage() {
  const empty = {
    title: '',
    description: '',
    startsAt: toLocalInput(new Date()),
    endsAt: toLocalInput(new Date(Date.now() + 30 * 86400000)),
    isActive: true,
    imagePreview: '',
    imageBase64: '',
    imageContentType: 'image/jpeg'
  }
  const [items, setItems] = useState([])
  const [open, setOpen] = useState(false)
  const [editId, setEditId] = useState(null)
  const [form, setForm] = useState(empty)
  const [msg, setMsg] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const load = () => api.get('/api/promotions/all')
    .then((r) => { setItems(r.data); setError('') })
    .catch((err) => setError(apiError(err, 'Не удалось загрузить акции (нужен Admin)')))
  useEffect(() => { load() }, [])

  function openCreate() {
    setEditId(null)
    setForm({ ...empty, startsAt: toLocalInput(new Date()), endsAt: toLocalInput(new Date(Date.now() + 30 * 86400000)) })
    setError('')
    setOpen(true)
  }

  function openEdit(p) {
    setEditId(p.id)
    setForm({
      title: p.title || '',
      description: p.description || '',
      startsAt: toLocalInput(p.startsAt),
      endsAt: toLocalInput(p.endsAt),
      isActive: !!p.isActive,
      imagePreview: p.imageUrl || '',
      imageBase64: '',
      imageContentType: 'image/jpeg'
    })
    setError('')
    setOpen(true)
  }

  async function savePromo(e) {
    e.preventDefault()
    setSaving(true)
    setMsg('')
    setError('')
    const payload = {
      title: form.title,
      description: form.description,
      startsAt: new Date(form.startsAt).toISOString(),
      endsAt: new Date(form.endsAt).toISOString(),
      isActive: form.isActive,
      imageBase64: form.imageBase64 || null,
      imageContentType: form.imageBase64 ? form.imageContentType : null
    }
    try {
      if (editId) {
        await api.put(`/api/promotions/${editId}`, payload)
        setMsg('Акция обновлена')
      } else {
        await api.post('/api/promotions', payload)
        setMsg('Акция добавлена')
      }
      setOpen(false)
      await load()
    } catch (err) {
      setError(apiError(err, 'Не удалось сохранить'))
    } finally {
      setSaving(false)
    }
  }

  async function removePromo(id) {
    if (!window.confirm('Удалить акцию?')) return
    setError('')
    try {
      await api.delete(`/api/promotions/${id}`)
      setMsg('Акция удалена')
      await load()
    } catch (err) {
      setError(apiError(err, 'Не удалось удалить'))
    }
  }

  return (
    <>
      <div className="panel">
        <PageToolbar title="Акции" subtitle="Промо-блоки в мобильном приложении" onAdd={openCreate} addLabel="Добавить акцию" />
        {msg && <p className="ok">{msg}</p>}
        {error && !open && <p className="error">{error}</p>}
        <table>
          <thead>
            <tr><th></th><th>Название</th><th>Период</th><th>Статус</th><th></th></tr>
          </thead>
          <tbody>
            {items.length === 0 && (
              <tr><td colSpan={5} className="hint">Нет акций — нажмите «+ Добавить акцию»</td></tr>
            )}
            {items.map((p) => (
              <tr key={p.id}>
                <td>{p.imageUrl ? <img src={p.imageUrl} alt="" className="table-thumb table-thumb--wide" /> : '—'}</td>
                <td>
                  <strong>{p.title}</strong>
                  <div className="hint">{p.description}</div>
                </td>
                <td>{new Date(p.startsAt).toLocaleString('ru-RU')} — {new Date(p.endsAt).toLocaleString('ru-RU')}</td>
                <td>{p.isActive ? 'Активна' : 'Выкл'}</td>
                <td className="table-actions">
                  <button className="btn ghost" type="button" onClick={() => openEdit(p)}>Изменить</button>
                  <button className="btn ghost" type="button" onClick={() => removePromo(p.id)}>Удалить</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal
        open={open}
        title={editId ? 'Редактирование акции' : 'Новая акция'}
        onClose={() => setOpen(false)}
        wide
        footer={(
          <>
            <button type="button" className="btn ghost" onClick={() => setOpen(false)}>Отмена</button>
            <button type="submit" form="promo-form" className="btn" disabled={saving}>
              {saving ? 'Сохранение…' : editId ? 'Сохранить' : 'Добавить акцию'}
            </button>
          </>
        )}
      >
        <form id="promo-form" onSubmit={savePromo}>
          <div className="form-split">
            <div className="form-split__media">
              <ImageUploadPreview
                value={form.imagePreview}
                onChange={({ dataUrl, contentType }) => setForm((f) => ({
                  ...f,
                  imagePreview: dataUrl,
                  imageBase64: dataUrl,
                  imageContentType: contentType
                }))}
                placeholder="Баннер акции"
              />
            </div>
            <div className="form-split__fields">
              <div className="field"><label>Название *</label><input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} /></div>
              <div className="field"><label>Описание</label><textarea rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
              <div className="field"><label>Начало *</label><input type="datetime-local" required value={form.startsAt} onChange={(e) => setForm({ ...form, startsAt: e.target.value })} /></div>
              <div className="field"><label>Окончание *</label><input type="datetime-local" required value={form.endsAt} onChange={(e) => setForm({ ...form, endsAt: e.target.value })} /></div>
              <div className="field">
                <label>Активна</label>
                <select value={form.isActive ? '1' : '0'} onChange={(e) => setForm({ ...form, isActive: e.target.value === '1' })}>
                  <option value="1">Да</option>
                  <option value="0">Нет</option>
                </select>
              </div>
            </div>
          </div>
          {error && <p className="error">{error}</p>}
        </form>
      </Modal>
    </>
  )
}

function Clients() {
  const emptyCreate = {
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
  const [open, setOpen] = useState(false)
  const [editId, setEditId] = useState(null)
  const [form, setForm] = useState(emptyCreate)
  const [msg, setMsg] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [resetPassword, setResetPassword] = useState({})

  const load = () => api.get('/api/admin/clients')
    .then((r) => { setItems(r.data); setError('') })
    .catch((err) => setError(apiError(err)))
  useEffect(() => { load() }, [])

  function setField(key, value) {
    setForm((f) => ({ ...f, [key]: value }))
  }

  function openCreate() {
    setEditId(null)
    setForm(emptyCreate)
    setError('')
    setOpen(true)
  }

  function openEdit(c) {
    setEditId(c.id)
    setForm({
      phone: c.phone || '',
      name: c.name || '',
      loyaltyPoints: c.loyaltyPoints ?? 0,
      loyaltyLevel: c.loyaltyLevel ?? 1,
      isActive: c.isActive !== false
    })
    setError('')
    setOpen(true)
  }

  async function saveClient(e) {
    e.preventDefault()
    setSaving(true)
    setMsg('')
    setError('')
    try {
      if (editId) {
        const { data } = await api.put(`/api/admin/clients/${editId}`, {
          phone: form.phone,
          name: form.name,
          loyaltyPoints: Number(form.loyaltyPoints),
          loyaltyLevel: Number(form.loyaltyLevel),
          isActive: form.isActive
        })
        setMsg(data.message || 'Профиль обновлён')
      } else {
        const { data } = await api.post('/api/admin/clients', form)
        setMsg(`${data.message}. Код: ${data.referralCode}`)
      }
      setOpen(false)
      await load()
    } catch (err) {
      setError(apiError(err, editId ? 'Не удалось сохранить' : 'Не удалось зарегистрировать'))
    } finally {
      setSaving(false)
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
      setError(apiError(err, 'Не удалось сбросить пароль'))
    }
  }

  const levelLabel = (lvl) => (lvl >= 3 ? 'Платина' : lvl === 2 ? 'Серебро' : 'Гость')

  return (
    <>
      <div className="panel">
        <PageToolbar title="Клиенты" subtitle="Регистрация и редактирование профилей" onAdd={openCreate} addLabel="Добавить клиента" />
        {msg && <p className="ok">{msg}</p>}
        {error && !open && <p className="error">{error}</p>}
        <table>
          <thead>
            <tr><th>Имя</th><th>Телефон</th><th>Баллы</th><th>Уровень</th><th>Реф.</th><th>Пароль</th><th></th></tr>
          </thead>
          <tbody>
            {items.length === 0 && (
              <tr><td colSpan={7} className="hint">Нет клиентов — нажмите «+ Добавить клиента»</td></tr>
            )}
            {items.map((c) => (
              <tr key={c.id}>
                <td>{c.name || '—'}</td>
                <td>{c.phone}</td>
                <td>{c.loyaltyPoints}</td>
                <td>{c.levelTitle || levelLabel(c.loyaltyLevel)}</td>
                <td>
                  <div>{c.referralCode}</div>
                  <div className="hint">приглашено: {c.referralCount ?? 0}</div>
                </td>
                <td>
                  <input
                    className="input-sm"
                    placeholder="новый"
                    value={resetPassword[c.id] || ''}
                    onChange={(e) => setResetPassword((s) => ({ ...s, [c.id]: e.target.value }))}
                  />
                  <button className="btn ghost" type="button" onClick={() => doResetPassword(c.id)}>OK</button>
                </td>
                <td>
                  <button className="btn ghost" type="button" onClick={() => openEdit(c)}>Изменить</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal
        open={open}
        title={editId ? 'Редактирование клиента' : 'Новый клиент'}
        onClose={() => setOpen(false)}
        wide
        footer={(
          <>
            <button type="button" className="btn ghost" onClick={() => setOpen(false)}>Отмена</button>
            <button type="submit" form="client-form" className="btn" disabled={saving}>
              {saving ? 'Сохранение…' : editId ? 'Сохранить' : 'Зарегистрировать'}
            </button>
          </>
        )}
      >
        <form id="client-form" onSubmit={saveClient}>
          {editId ? (
            <div className="form-grid-modal">
              <div className="field"><label>Телефон *</label><input required value={form.phone} onChange={(e) => setField('phone', e.target.value)} /></div>
              <div className="field"><label>Имя</label><input value={form.name} onChange={(e) => setField('name', e.target.value)} /></div>
              <div className="field"><label>Бонусные баллы</label><input type="number" min="0" required value={form.loyaltyPoints} onChange={(e) => setField('loyaltyPoints', e.target.value)} /></div>
              <div className="field">
                <label>Уровень лояльности</label>
                <select value={form.loyaltyLevel} onChange={(e) => setField('loyaltyLevel', Number(e.target.value))}>
                  <option value={1}>Гость</option>
                  <option value={2}>Серебро</option>
                  <option value={3}>Платина</option>
                </select>
              </div>
              <div className="field">
                <label>Активен</label>
                <select value={form.isActive ? '1' : '0'} onChange={(e) => setField('isActive', e.target.value === '1')}>
                  <option value="1">Да</option>
                  <option value="0">Нет</option>
                </select>
              </div>
            </div>
          ) : (
            <div className="form-grid-modal">
              <div className="field"><label>Телефон *</label><input required value={form.phone} onChange={(e) => setField('phone', e.target.value)} placeholder="+79001234567" /></div>
              <div className="field"><label>Пароль *</label><input required minLength={4} type="password" value={form.password} onChange={(e) => setField('password', e.target.value)} /></div>
              <div className="field"><label>Имя</label><input value={form.name} onChange={(e) => setField('name', e.target.value)} /></div>
              <div className="field"><label>Реф. код друга</label><input value={form.referralCode} onChange={(e) => setField('referralCode', e.target.value)} /></div>
              <div className="field"><label>Марка авто</label><input value={form.vehicleBrand} onChange={(e) => setField('vehicleBrand', e.target.value)} /></div>
              <div className="field"><label>Модель</label><input value={form.vehicleModel} onChange={(e) => setField('vehicleModel', e.target.value)} /></div>
              <div className="field"><label>Госномер</label><input value={form.plateNumber} onChange={(e) => setField('plateNumber', e.target.value)} /></div>
              <div className="field">
                <label>Тип кузова</label>
                <select value={form.vehicleType} onChange={(e) => setField('vehicleType', Number(e.target.value))}>
                  <option value={0}>Седан</option>
                  <option value={1}>Кроссовер</option>
                  <option value={2}>Внедорожник</option>
                  <option value={3}>Внедорожник XL</option>
                </select>
              </div>
            </div>
          )}
          {error && <p className="error">{error}</p>}
        </form>
      </Modal>
    </>
  )
}

function Staff() {
  const emptyForm = { phone: '+79', password: '', name: '', role: 'Master' }
  const [items, setItems] = useState([])
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState(emptyForm)
  const [msg, setMsg] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [resetPassword, setResetPassword] = useState({})

  const load = () => api.get('/api/admin/staff')
    .then((r) => { setItems(r.data); setError('') })
    .catch((err) => setError(apiError(err, 'Нет доступа (нужен Admin)')))
  useEffect(() => { load() }, [])

  function openModal() {
    setForm(emptyForm)
    setError('')
    setOpen(true)
  }

  async function createStaff(e) {
    e.preventDefault()
    setSaving(true)
    setMsg('')
    setError('')
    try {
      const { data } = await api.post('/api/admin/staff', form)
      setMsg(data.message)
      setOpen(false)
      setForm(emptyForm)
      await load()
    } catch (err) {
      setError(apiError(err, 'Не удалось создать'))
    } finally {
      setSaving(false)
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
      setError(apiError(err, 'Не удалось сбросить пароль'))
    }
  }

  return (
    <>
      <div className="panel">
        <PageToolbar title="Сотрудники" onAdd={openModal} addLabel="Добавить сотрудника" />
        {msg && <p className="ok">{msg}</p>}
        {error && !open && <p className="error">{error}</p>}
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

      <Modal
        open={open}
        title="Новый сотрудник"
        onClose={() => setOpen(false)}
        footer={(
          <>
            <button type="button" className="btn ghost" onClick={() => setOpen(false)}>Отмена</button>
            <button type="submit" form="staff-form" className="btn" disabled={saving}>
              {saving ? 'Сохранение…' : 'Добавить'}
            </button>
          </>
        )}
      >
        <form id="staff-form" onSubmit={createStaff}>
          <div className="form-grid-modal">
            <div className="field"><label>Телефон *</label><input required value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} /></div>
            <div className="field"><label>Пароль *</label><input required minLength={4} type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></div>
            <div className="field"><label>Имя</label><input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></div>
            <div className="field">
              <label>Роль *</label>
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
                <option value="Master">Мастер</option>
                <option value="Admin">Администратор</option>
              </select>
            </div>
          </div>
          {error && <p className="error">{error}</p>}
        </form>
      </Modal>
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
      <Route path="/promotions" element={<PrivateRoute><PromotionsPage /></PrivateRoute>} />
      <Route path="/clients" element={<PrivateRoute><Clients /></PrivateRoute>} />
      <Route path="/staff" element={<PrivateRoute><Staff /></PrivateRoute>} />
    </Routes>
  )
}
