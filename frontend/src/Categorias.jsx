import { useEffect, useState } from 'react'
import { api } from './api.js'

function Categorias() {
  const [categorias, setCategorias] = useState([])
  const [novoNome, setNovoNome] = useState('')
  const [editandoId, setEditandoId] = useState(null)
  const [nomeEditado, setNomeEditado] = useState('')
  const [erro, setErro] = useState('')

  async function carregar() {
    try {
      setCategorias(await api('/categorias'))
    } catch (e) {
      setErro(e.message)
    }
  }

  useEffect(() => {
    api('/categorias')
      .then(setCategorias)
      .catch((e) => setErro(e.message))
  }, [])

  // Executa uma ação na API, mostra o erro se houver e recarrega a lista.
  async function executar(acao) {
    setErro('')
    try {
      await acao()
      await carregar()
      return true
    } catch (e) {
      setErro(e.message)
      return false
    }
  }

  async function criar(evento) {
    evento.preventDefault()
    const ok = await executar(() =>
      api('/categorias', { method: 'POST', body: { nome: novoNome } }),
    )
    if (ok) setNovoNome('')
  }

  function comecarEdicao(categoria) {
    setEditandoId(categoria.id)
    setNomeEditado(categoria.nome)
    setErro('')
  }

  async function salvarEdicao(evento) {
    evento.preventDefault()
    const ok = await executar(() =>
      api(`/categorias/${editandoId}`, { method: 'PUT', body: { nome: nomeEditado } }),
    )
    if (ok) setEditandoId(null)
  }

  async function excluir(categoria) {
    if (!confirm(`Excluir a categoria "${categoria.nome}"?`)) return
    await executar(() => api(`/categorias/${categoria.id}`, { method: 'DELETE' }))
  }

  return (
    <section>
      <h2>Categorias</h2>

      <form onSubmit={criar} className="linha">
        <input
          value={novoNome}
          onChange={(e) => setNovoNome(e.target.value)}
          placeholder="Nova categoria"
        />
        <button type="submit">Adicionar</button>
      </form>

      {erro && <p className="erro">{erro}</p>}

      {categorias.length === 0 && <p>Nenhuma categoria cadastrada.</p>}

      <ul className="lista">
        {categorias.map((categoria) =>
          categoria.id === editandoId ? (
            <li key={categoria.id}>
              <form onSubmit={salvarEdicao} className="linha">
                <input
                  value={nomeEditado}
                  onChange={(e) => setNomeEditado(e.target.value)}
                  autoFocus
                />
                <button type="submit">Salvar</button>
                <button type="button" onClick={() => setEditandoId(null)}>
                  Cancelar
                </button>
              </form>
            </li>
          ) : (
            <li key={categoria.id} className="linha">
              <span>{categoria.nome}</span>
              <button onClick={() => comecarEdicao(categoria)}>Renomear</button>
              <button onClick={() => excluir(categoria)}>Excluir</button>
            </li>
          ),
        )}
      </ul>
    </section>
  )
}

export default Categorias
