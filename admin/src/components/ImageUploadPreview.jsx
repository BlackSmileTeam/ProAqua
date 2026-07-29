import { useRef } from 'react'

export default function ImageUploadPreview({ value, onChange, label = 'Превью', placeholder = 'Картинка не выбрана' }) {
  const inputRef = useRef(null)

  function pick() {
    inputRef.current?.click()
  }

  async function onFile(e) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = () => {
      onChange?.({ dataUrl: String(reader.result || ''), contentType: file.type || 'image/jpeg' })
    }
    reader.readAsDataURL(file)
    e.target.value = ''
  }

  return (
    <div className="image-upload">
      <input ref={inputRef} type="file" accept="image/*" hidden onChange={onFile} />
      <div className="image-upload__frame">
        {value ? (
          <img src={value} alt="" className="image-upload__img" />
        ) : (
          <div className="image-upload__placeholder">
            <span className="image-upload__icon">🖼</span>
            <span>{placeholder}</span>
          </div>
        )}
      </div>
      <button type="button" className="btn ghost image-upload__btn" onClick={pick}>
        {value ? 'Изменить' : 'Выбрать картинку'}
      </button>
    </div>
  )
}
