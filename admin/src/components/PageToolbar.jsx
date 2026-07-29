export default function PageToolbar({ title, subtitle, onAdd, addLabel = 'Добавить' }) {
  return (
    <div className="page-toolbar">
      <div>
        <h2 className="page-toolbar__title">{title}</h2>
        {subtitle && <p className="hint page-toolbar__subtitle">{subtitle}</p>}
      </div>
      {onAdd && (
        <button type="button" className="btn btn-add" onClick={onAdd} title={addLabel}>
          <span className="btn-add__plus">+</span>
          <span>{addLabel}</span>
        </button>
      )}
    </div>
  )
}
