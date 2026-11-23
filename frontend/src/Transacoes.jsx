import { useEffect, useState } from 'react'
import { api } from './api.js'
import { baixarArquivo, gerarCsv } from './csv.js'
import { formatarData, formatarReais, hoje, normalizar, previaDeParcelas, situacaoDe } from './formatar.js'

const formularioVazio = () => ({
  descricao: '',
  valor: '',
  data: hoje(),
  tipo: 'Despesa',
  categoriaId: '',
  pago: true,
})

// Situação clicável na tabela: ✔ Paga / ○ A pagar / ⚠ Atrasada (ícone + texto, não só cor).
function BotaoSituacao({ transacao, aoClicar }) {
  const texto = situacaoDe(transacao)
  const [classe, icone] = transacao.pago
    ? ['paga', '✔']
    : texto === 'Atrasada'
      ? ['atrasada', '⚠']
      : ['pendente', '○']
  return (
    <button
      type="button"
      className={`situacao ${classe}`}
      onClick={aoClicar}
      title={transacao.pago ? 'Clique para marcar como pendente' : 'Clique para marcar como paga'}
    >
      {icone} {texto}
    </button>
  )
}

function Transacoes({ mes, atualizacao, aoAlterar }) {
  const [transacoes, setTransacoes] = useState([])
  const [categorias, setCategorias] = useState([])
  const [formulario, setFormulario] = useState(formularioVazio)
  const [editandoId, setEditandoId] = useState(null)
  const [erro, setErro] = useState('')
  // Ao criar: só este mês, repetir todo mês ou parcelar o valor; e quantas vezes.
  const [lancamento, setLancamento] = useState('unico')
  const [vezes, setVezes] = useState('12')
  // Editando uma transação de série? E aplicar a mudança aos meses seguintes?
  const [editandoSerie, setEditandoSerie] = useState(false)
  const [aplicarSeguintes, setAplicarSeguintes] = useState(false)
  // Filtros da lista (só escondem linhas; não chamam a API)
  const [busca, setBusca] = useState('')
  const [filtroCategoria, setFiltroCategoria] = useState('')
  const [filtroTipo, setFiltroTipo] = useState('')
  const [filtroSituacao, setFiltroSituacao] = useState('')

  // Recarrega sempre que algo mudar em qualquer parte da tela ou o mês mudar.
  useEffect(() => {
    Promise.all([api(mes ? `/transacoes?mes=${mes}` : '/transacoes'), api('/categorias')])
      .then(([listaTransacoes, listaCategorias]) => {
        setTransacoes(listaTransacoes)
        setCategorias(listaCategorias)
      })
      .catch((e) => setErro(e.message))
  }, [mes, atualizacao])

  const previaParcelas = previaDeParcelas(formulario.valor, vezes)

  const termo = normalizar(busca.trim())
  const filtradas = transacoes.filter(
    (t) =>
      normalizar(t.descricao).includes(termo) &&
      (!filtroCategoria || t.categoriaId === Number(filtroCategoria)) &&
      (!filtroTipo || t.tipo === filtroTipo) &&
      (!filtroSituacao || (filtroSituacao === 'pagas' ? t.pago : !t.pago)),
  )
  const filtrando = busca || filtroCategoria || filtroTipo || filtroSituacao
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
    setFiltroSituacao('')
  }

  function alterarCampo(evento) {
    const { name, value } = evento.target
    // Ao criar: data de hoje ou passada já vem como paga; data futura, como pendente.
    const pago = name === 'data' && !editandoId ? value <= hoje() : formulario.pago
    setFormulario({ ...formulario, [name]: value, pago })
  }

  function cancelarEdicao() {
    setEditandoId(null)
    setFormulario(formularioVazio())
    setLancamento('unico')
    setVezes('12')
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
        const extra = { unico: '', repetir: `?repetir=${vezes}`, parcelar: `?parcelas=${vezes}` }
        await api(`/transacoes${extra[lancamento]}`, { method: 'POST', body: corpo })
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
      pago: transacao.pago,
    })
    setEditandoSerie(transacao.serieId != null)
    setAplicarSeguintes(false)
    setErro('')
  }

  // Clique na situação: alterna entre paga e pendente.
  async function alternarPago(transacao) {
    setErro('')
    try {
      await api(`/transacoes/${transacao.id}/pago`, { method: 'PUT', body: { pago: !transacao.pago } })
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    }
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
            {!editandoId && lancamento === 'parcelar' ? 'Valor total (R$)' : 'Valor (R$)'}
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
              Lançamento
              <select value={lancamento} onChange={(e) => setLancamento(e.target.value)}>
                <option value="unico">Uma vez</option>
                <option value="repetir">Repetir</option>
                <option value="parcelar">Parcelar</option>
              </select>
            </label>
          )}
          {!editandoId && lancamento !== 'unico' && (
            <label className="campo">
              {lancamento === 'repetir' ? 'Quantos meses' : 'Parcelas'}
              <input
                type="number"
                min={lancamento === 'repetir' ? 1 : 2}
                max="60"
                value={vezes}
                onChange={(e) => setVezes(e.target.value)}
                required
              />
            </label>
          )}
          {!editandoId && lancamento === 'parcelar' && previaParcelas && (
            <p className="previa-parcelas">{previaParcelas}</p>
          )}
          <label className="seguintes">
            <input
              type="checkbox"
              checked={formulario.pago}
              onChange={(e) => setFormulario({ ...formulario, pago: e.target.checked })}
            />
            {formulario.tipo === 'Receita' ? 'Já foi recebida' : 'Já foi paga'}
            {!editandoId && lancamento !== 'unico' && (
              <small className="texto-suave">(só a 1ª; as dos meses seguintes começam pendentes)</small>
            )}
          </label>
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
              <select
                value={filtroSituacao}
                onChange={(e) => setFiltroSituacao(e.target.value)}
                aria-label="Filtrar por situação"
              >
                <option value="">Pagas e pendentes</option>
                <option value="pagas">Só pagas</option>
                <option value="pendentes">Só pendentes</option>
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
                      <th>Situação</th>
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
                        <td>
                          <BotaoSituacao transacao={transacao} aoClicar={() => alternarPago(transacao)} />
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
