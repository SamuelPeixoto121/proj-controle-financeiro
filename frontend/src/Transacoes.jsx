import { useEffect, useState } from 'react'
import { api } from './api.js'
import { baixarArquivo, gerarCsv } from './csv.js'
import { formatarData, formatarReais, hoje, normalizar } from './formatar.js'

const formularioVazio = () => ({
  descricao: '',
  valor: '',
  data: hoje(),
  tipo: 'Despesa',
  categoriaId: '',
})

function Transacoes({ mes, atualizacao, aoAlterar }) {
  const [transacoes, setTransacoes] = useState([])
  const [categorias, setCategorias] = useState([])
  const [formulario, setFormulario] = useState(formularioVazio)
  const [editandoId, setEditandoId] = useState(null)
  const [erro, setErro] = useState('')
  // Quantos meses repetir ao criar (1 = só este mês)
  const [repetir, setRepetir] = useState('1')
  // Editando uma transação de série? E aplicar a mudança aos meses seguintes?
  const [editandoSerie, setEditandoSerie] = useState(false)
  const [aplicarSeguintes, setAplicarSeguintes] = useState(false)
  // Filtros da lista (só escondem linhas; não chamam a API)
  const [busca, setBusca] = useState('')
  const [filtroCategoria, setFiltroCategoria] = useState('')
  const [filtroTipo, setFiltroTipo] = useState('')

  // Recarrega sempre que algo mudar em qualquer parte da tela ou o mês mudar.
  useEffect(() => {
    Promise.all([api(mes ? `/transacoes?mes=${mes}` : '/transacoes'), api('/categorias')])
      .then(([listaTransacoes, listaCategorias]) => {
        setTransacoes(listaTransacoes)
        setCategorias(listaCategorias)
      })
      .catch((e) => setErro(e.message))
  }, [mes, atualizacao])

  const termo = normalizar(busca.trim())
  const filtradas = transacoes.filter(
    (t) =>
      normalizar(t.descricao).includes(termo) &&
      (!filtroCategoria || t.categoriaId === Number(filtroCategoria)) &&
      (!filtroTipo || t.tipo === filtroTipo),
  )
  const filtrando = busca || filtroCategoria || filtroTipo
  const somar = (tipo) =>
    filtradas.filter((t) => t.tipo === tipo).reduce((soma, t) => soma + t.valor, 0)

  // Baixa o que está na tabela agora (mês + filtros).
  function exportar() {
    baixarArquivo(`transacoes-${mes || 'todos-os-meses'}.csv`, gerarCsv(filtradas))
  }

  function limparFiltros() {
    setBusca('')
    setFiltroCategoria('')
    setFiltroTipo('')
  }

  function alterarCampo(evento) {
    const { name, value } = evento.target
    setFormulario({ ...formulario, [name]: value })
  }

  function cancelarEdicao() {
    setEditandoId(null)
    setFormulario(formularioVazio())
    setRepetir('1')
    setEditandoSerie(false)
    setAplicarSeguintes(false)
    setErro('')
  }

  async function salvar(evento) {
    evento.preventDefault()
    setErro('')
    const corpo = {
      ...formulario,
      valor: Number(formulario.valor),
      categoriaId: Number(formulario.categoriaId),
    }
    try {
      if (editandoId) {
        const seguintes = aplicarSeguintes ? '?seguintes=true' : ''
        await api(`/transacoes/${editandoId}${seguintes}`, { method: 'PUT', body: corpo })
      } else {
        await api(`/transacoes?repetir=${repetir}`, { method: 'POST', body: corpo })
      }
      cancelarEdicao()
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    }
  }

  function editar(transacao) {
    setEditandoId(transacao.id)
    setFormulario({
      descricao: transacao.descricao,
      valor: String(transacao.valor),
      data: transacao.data.slice(0, 10),
      tipo: transacao.tipo,
      categoriaId: String(transacao.categoriaId),
    })
    setEditandoSerie(transacao.serieId != null)
    setAplicarSeguintes(false)
    setErro('')
  }

  async function excluir(transacao) {
    if (!confirm(`Excluir a transação "${transacao.descricao}"?`)) return
    // Se for de uma série, pergunta se exclui também os meses seguintes.
    const seguintes =
      transacao.serieId != null &&
      confirm('Excluir também os meses seguintes desta série?\n\nOK = sim, Cancelar = só esta')
    setErro('')
    try {
      await api(`/transacoes/${transacao.id}${seguintes ? '?seguintes=true' : ''}`, { method: 'DELETE' })
      if (seguintes || transacao.id === editandoId) cancelarEdicao()
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    }
  }

  return (
    <>
      <section className="card">
        <h2>{editandoId ? 'Editar transação' : 'Nova transação'}</h2>

        <form onSubmit={salvar} className="formulario">
          <label className="campo campo-largo">
            Descrição
            <input
              name="descricao"
              value={formulario.descricao}
              onChange={alterarCampo}
              placeholder="Ex.: Mercado, Salário..."
            />
          </label>
          <label className="campo">
            Valor (R$)
            <input
              name="valor"
              type="number"
              step="0.01"
              min="0.01"
              value={formulario.valor}
              onChange={alterarCampo}
              placeholder="0,00"
            />
          </label>
          <label className="campo">
            Data
            <input name="data" type="date" value={formulario.data} onChange={alterarCampo} />
          </label>
          <label className="campo">
            Tipo
            <select name="tipo" value={formulario.tipo} onChange={alterarCampo}>
              <option value="Despesa">Despesa</option>
              <option value="Receita">Receita</option>
            </select>
          </label>
          <label className="campo">
            Categoria
            <select name="categoriaId" value={formulario.categoriaId} onChange={alterarCampo}>
              <option value="">Escolha...</option>
              {categorias.map((categoria) => (
                <option key={categoria.id} value={categoria.id}>
                  {categoria.nome}
                </option>
              ))}
            </select>
          </label>
          {!editandoId && (
            <label className="campo">
              Repetir por ({repetir === '1' ? 'mês' : 'meses'})
              <input
                type="number"
                min="1"
                max="60"
                value={repetir}
                onChange={(e) => setRepetir(e.target.value)}
                required
              />
            </label>
          )}
          {editandoSerie && (
            <label className="seguintes">
              <input
                type="checkbox"
                checked={aplicarSeguintes}
                onChange={(e) => setAplicarSeguintes(e.target.checked)}
              />
              Aplicar também aos meses seguintes desta série
            </label>
          )}

          <div className="formulario-botoes">
            <button type="submit" className="principal">
              {editandoId ? 'Salvar' : 'Adicionar'}
            </button>
            {editandoId && (
              <button type="button" onClick={cancelarEdicao}>
                Cancelar
              </button>
            )}
          </div>
        </form>

        {erro && <p className="erro">{erro}</p>}
      </section>

      <section className="card">
        <h2>Transações</h2>

        {transacoes.length === 0 ? (
          <p className="vazio">
            {mes ? 'Nenhuma transação neste mês.' : 'Nenhuma transação cadastrada.'}
          </p>
        ) : (
          <>
            <div className="filtros">
              <input
                type="search"
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
                placeholder="🔍 Buscar descrição..."
                aria-label="Buscar descrição"
              />
              <select
                value={filtroCategoria}
                onChange={(e) => setFiltroCategoria(e.target.value)}
                aria-label="Filtrar por categoria"
              >
                <option value="">Todas as categorias</option>
                {categorias.map((categoria) => (
                  <option key={categoria.id} value={categoria.id}>
                    {categoria.nome}
                  </option>
                ))}
              </select>
              <select
                value={filtroTipo}
                onChange={(e) => setFiltroTipo(e.target.value)}
                aria-label="Filtrar por tipo"
              >
                <option value="">Todos os tipos</option>
                <option value="Receita">Receitas</option>
                <option value="Despesa">Despesas</option>
              </select>
              {filtrando && (
                <button type="button" onClick={limparFiltros}>
                  Limpar
                </button>
              )}
            </div>

            {filtradas.length === 0 ? (
              <p className="vazio">Nenhuma transação encontrada com esses filtros.</p>
            ) : (
              <div className="tabela-rolagem">
                <table className="tabela-transacoes">
                  <thead>
                    <tr>
                      <th>Data</th>
                      <th>Descrição</th>
                      <th>Categoria</th>
                      <th>Tipo</th>
                      <th className="valor">Valor</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtradas.map((transacao) => (
                      <tr key={transacao.id} className={transacao.id === editandoId ? 'editando' : ''}>
                        <td>{formatarData(transacao.data)}</td>
                        <td>
                          {transacao.descricao}
                          {transacao.serieId && (
                            <span className="serie" title="Faz parte de uma série repetida">
                              {' '}
                              ↻
                            </span>
                          )}
                        </td>
                        <td>{transacao.categoria?.nome}</td>
                        <td>
                          <span className={`etiqueta ${transacao.tipo === 'Receita' ? 'receita' : 'despesa'}`}>
                            {transacao.tipo}
                          </span>
                        </td>
                        <td className={`valor ${transacao.tipo === 'Receita' ? 'receita' : 'despesa'}`}>
                          {transacao.tipo === 'Receita' ? '+ ' : '− '}
                          {formatarReais(transacao.valor)}
                        </td>
                        <td className="acoes">
                          <button onClick={() => editar(transacao)}>Editar</button>
                          <button className="perigo" onClick={() => excluir(transacao)}>
                            Excluir
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {filtradas.length > 0 && (
              <div className="rodape-lista">
                <span>
                  {filtradas.length} {filtradas.length === 1 ? 'transação' : 'transações'} · Receitas{' '}
                  <span className="receita">{formatarReais(somar('Receita'))}</span> · Despesas{' '}
                  <span className="despesa">{formatarReais(somar('Despesa'))}</span>
                </span>
                <button type="button" onClick={exportar}>
                  ⬇ Exportar CSV
                </button>
              </div>
            )}
          </>
        )}
      </section>
    </>
  )
}

export default Transacoes
