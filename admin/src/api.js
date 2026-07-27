import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || ''
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('proaqua_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

export function setToken(token) {
  localStorage.setItem('proaqua_token', token)
}

export function getToken() {
  return localStorage.getItem('proaqua_token')
}

export function clearToken() {
  localStorage.removeItem('proaqua_token')
}

export default api
