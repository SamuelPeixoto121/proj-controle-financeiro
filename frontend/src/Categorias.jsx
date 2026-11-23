import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarReais } from './formatar.js'

function Categorias({ aoAlterar }) {
  const [categorias, setCategorias] = useState([])
  const [novoNome, setNovoNome] = useState('')
  const [editandoId, setEditandoId] = useState(null)
  const [nomeEditado, setNomeEditado] = useState('')
  const [metaEditada, setMetaEditada] = useState('') // vazio = sem meta
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
      aoAlterar()
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
    setMetaEditada(categoria.metaMensal == null ? '' : String(categoria.metaMensal))
    setErro('')
  }

  async function salvarEdicao(evento) {
    evento.preventDefault()
    const ok = await executar(() =>
      api(`/categorias/${editandoId}`, {
        method: 'PUT',
        body: {
          nome: nomeEditado,
          metaMensal: metaEditada === '' ? null : Number(metaEditada),
        },
      }),
    )
    if (ok) setEditandoId(null)
  }

  async function excluir(categoria) {
    if (!confirm(`Excluir a categoria "${categoria.nome}"?`)) return
    await executar(() => api(`/categorias/${categoria.id}`, { method: 'DELETE' }))
  }

  return (
    <section className="card">
      <h2>Categorias</h2>
      <p className="texto-suave">
        Organize suas transações por categoria. Clique em Editar para definir uma meta de gasto por mês.
      </p>

      <form onSubmit={criar} className="linha nova-categoria">
        <input
          value={novoNome}
          onChange={(e) => setNovoNome(e.target.value)}
          placeholder="Nova categoria"
        />
        <button type="submit" className="principal">
          Adicionar
        </button>
      </form>

      {erro && <p className="erro">{erro}</p>}

      {categorias.length === 0 && <p className="vazio">Nenhuma categoria cadastrada.</p>}

      <ul className="lista">
        {categorias.map((categoria) =>
          categoria.id === editandoId ? (
            <li key={categoria.id}>
              <form onSubmit={salvarEdicao} className="linha">
                <input
                  value={nomeEditado}
                  onChange={(e) => setNomeEditado(e.target.value)}
                  autoFocus
                  aria-label="Nome"
                />
                <input
                  className="meta-input"
                  type="number"
                  step="0.01"
                  min="0.01"
                  value={metaEditada}
                  onChange={(e) => setMetaEditada(e.target.value)}
                  placeholder="Meta mensal"
                  aria-label="Meta mensal (vazio = sem meta)"
                />
                <button type="submit" className="principal">
                  Salvar
                </button>
                <button type="button" onClick={() => setEditandoId(null)}>
                  Cancelar
                </button>
              </form>
            </li>
          ) : (
            <li key={categoria.id} className="linha">
              <span className="categoria-nome">
                {categoria.nome}
                {categoria.metaMensal != null && (
                  <small className="meta-info">meta {formatarReais(categoria.metaMensal)}/mês</small>
                )}
              </span>
              <button onClick={() => comecarEdicao(categoria)}>Editar</button>
              <button className="perigo" onClick={() => excluir(categoria)}>
                Excluir
              </button>
            </li>
          ),
        )}
      </ul>
    </section>
  )
}

export default Categorias
