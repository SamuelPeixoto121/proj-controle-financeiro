import { useEffect, useState } from 'react'
import { api } from './api.js'
import Cartoes from './Cartoes.jsx'
import { formatarData, formatarReais, hoje } from './formatar.js'

const corDoValor = (valor) => (valor < 0 ? 'despesa' : valor > 0 ? 'receita' : '')
const transferenciaVazia = () => ({ contaOrigemId: '', contaDestinoId: '', valor: '', data: hoje(), descricao: '' })

// Aba Contas: o saldo de cada conta, as transferências entre elas e os cartões de crédito.
function Contas({ atualizacao, aoAlterar }) {
  const [contas, setContas] = useState([])
  const [transferencias, setTransferencias] = useState([])
  const [nova, setNova] = useState({ nome: '', saldoInicial: '' })
  const [editandoId, setEditandoId] = useState(null)
  const [editada, setEditada] = useState({ nome: '', saldoInicial: '' })
  const [transferencia, setTransferencia] = useState(transferenciaVazia)
  const [erro, setErro] = useState('')
  const [erroTransferencia, setErroTransferencia] = useState('')

  useEffect(() => {
    Promise.all([api('/contas'), api('/transferencias')])
      .then(([listaContas, listaTransferencias]) => {
        setContas(listaContas)
        setTransferencias(listaTransferencias)
      })
      .catch((e) => setErro(e.message))
  }, [atualizacao])

  // Executa uma ação na API, mostra o erro (no lugar certo) e avisa o resto da tela.
  async function executar(acao, mostrarErro = setErro) {
    setErro('')
    setErroTransferencia('')
    try {
      await acao()
      aoAlterar()
      return true
    } catch (e) {
      mostrarErro(e.message)
      return false
    }
  }

  const corpoDaConta = (c) => ({ nome: c.nome, saldoInicial: Number(c.saldoInicial || 0) })

  async function criar(evento) {
    evento.preventDefault()
    if (await executar(() => api('/contas', { method: 'POST', body: corpoDaConta(nova) })))
      setNova({ nome: '', saldoInicial: '' })
  }

  async function salvarEdicao(evento) {
    evento.preventDefault()
    if (await executar(() => api(`/contas/${editandoId}`, { method: 'PUT', body: corpoDaConta(editada) })))
      setEditandoId(null)
  }

  async function excluir(conta) {
    if (!confirm(`Excluir a conta "${conta.nome}"?`)) return
    await executar(() => api(`/contas/${conta.id}`, { method: 'DELETE' }))
  }

  async function transferir(evento) {
    evento.preventDefault()
    const corpo = {
      ...transferencia,
      valor: Number(transferencia.valor),
      contaOrigemId: Number(transferencia.contaOrigemId),
      contaDestinoId: Number(transferencia.contaDestinoId),
    }
    if (await executar(() => api('/transferencias', { method: 'POST', body: corpo }), setErroTransferencia))
      setTransferencia(transferenciaVazia())
  }

  async function excluirTransferencia(t) {
    if (!confirm(`Excluir a transferência de ${formatarReais(t.valor)} (${t.origem} → ${t.destino})?`)) return
    await executar(() => api(`/transferencias/${t.id}`, { method: 'DELETE' }), setErroTransferencia)
  }

  const mudarTransferencia = (campo) => (e) => setTransferencia({ ...transferencia, [campo]: e.target.value })
  const total = contas.reduce((soma, c) => soma + c.saldo, 0)

  return (
    <>
      <section className="card">
        <h2>Contas</h2>
        <p className="texto-suave">
          Onde está o seu dinheiro. O saldo de cada conta é o saldo inicial mais o que já foi recebido, menos o que já
          foi pago, mais as transferências.
        </p>

        <form onSubmit={criar} className="formulario">
          <label className="campo campo-largo">
            Nova conta
            <input
              value={nova.nome}
              onChange={(e) => setNova({ ...nova, nome: e.target.value })}
              placeholder="Ex.: Itaú, Carteira, Poupança"
            />
          </label>
          <label className="campo">
            Saldo inicial (R$)
            <input
              type="number"
              step="0.01"
              value={nova.saldoInicial}
              onChange={(e) => setNova({ ...nova, saldoInicial: e.target.value })}
              placeholder="0,00"
            />
          </label>
          <div className="formulario-botoes">
            <button type="submit" className="principal">
              Adicionar
            </button>
          </div>
        </form>

        {erro && <p className="erro">{erro}</p>}

        <ul className="lista contas-lista">
          {contas.map((conta) =>
            conta.id === editandoId ? (
              <li key={conta.id}>
                <form onSubmit={salvarEdicao} className="formulario">
                  <label className="campo campo-largo">
                    Nome
                    <input
                      value={editada.nome}
                      onChange={(e) => setEditada({ ...editada, nome: e.target.value })}
                      autoFocus
                    />
                  </label>
                  <label className="campo">
                    Saldo inicial (R$)
                    <input
                      type="number"
                      step="0.01"
                      value={editada.saldoInicial}
                      onChange={(e) => setEditada({ ...editada, saldoInicial: e.target.value })}
                    />
                  </label>
                  <div className="formulario-botoes">
                    <button type="submit" className="principal">
                      Salvar
                    </button>
                    <button type="button" onClick={() => setEditandoId(null)}>
                      Cancelar
                    </button>
                  </div>
                </form>
              </li>
            ) : (
              <li key={conta.id} className="linha">
                <span className="categoria-nome">
                  {conta.nome}
                  {conta.saldoInicial !== 0 && (
                    <small className="meta-info">inicial {formatarReais(conta.saldoInicial)}</small>
                  )}
                </span>
                <strong className={`valor ${corDoValor(conta.saldo)}`}>{formatarReais(conta.saldo)}</strong>
                <span className="acoes">
                  <button
                    onClick={() => {
                      setEditandoId(conta.id)
                      setEditada({ nome: conta.nome, saldoInicial: String(conta.saldoInicial) })
                      setErro('')
                    }}
                  >
                    Editar
                  </button>
                  <button className="perigo" onClick={() => excluir(conta)}>
                    Excluir
                  </button>
                </span>
              </li>
            ),
          )}
          {contas.length > 1 && (
            <li className="linha contas-total">
              <span className="categoria-nome">Total</span>
              <strong className={`valor ${corDoValor(total)}`}>{formatarReais(total)}</strong>
              <span className="acoes" />
            </li>
          )}
        </ul>
      </section>

      <section className="card">
        <h2>Transferir entre contas</h2>
        {contas.length < 2 ? (
          <p className="vazio">Cadastre uma segunda conta para transferir dinheiro entre elas.</p>
        ) : (
          <>
            <p className="texto-suave">
              Transferência não é receita nem despesa: só tira de uma conta e põe na outra. Com data futura, fica
              agendada e só muda os saldos quando o dia chegar.
            </p>
            <form onSubmit={transferir} className="formulario">
              <label className="campo">
                De
                <select value={transferencia.contaOrigemId} onChange={mudarTransferencia('contaOrigemId')}>
                  <option value="">Escolha...</option>
                  {contas.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nome}
                    </option>
                  ))}
                </select>
              </label>
              <label className="campo">
                Para
                <select value={transferencia.contaDestinoId} onChange={mudarTransferencia('contaDestinoId')}>
                  <option value="">Escolha...</option>
                  {contas.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nome}
                    </option>
                  ))}
                </select>
              </label>
              <label className="campo">
                Valor (R$)
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
                  value={transferencia.valor}
                  onChange={mudarTransferencia('valor')}
                  placeholder="0,00"
                />
              </label>
              <label className="campo">
                Data
                <input type="date" value={transferencia.data} onChange={mudarTransferencia('data')} />
              </label>
              <label className="campo campo-largo">
                Descrição (opcional)
                <input
                  value={transferencia.descricao}
                  onChange={mudarTransferencia('descricao')}
                  placeholder="Ex.: Guardar na poupança"
                />
              </label>
              <div className="formulario-botoes">
                <button type="submit" className="principal">
                  Transferir
                </button>
              </div>
            </form>
          </>
        )}

        {erroTransferencia && <p className="erro">{erroTransferencia}</p>}

        {transferencias.length > 0 && (
          <div className="tabela-rolagem">
            <table className="tabela-transferencias">
              <thead>
                <tr>
                  <th>Data</th>
                  <th>De → Para</th>
                  <th>Descrição</th>
                  <th className="valor">Valor</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {transferencias.map((t) => (
                  <tr key={t.id}>
                    <td>
                      {formatarData(t.data)}
                      {t.agendada && <small className="situacao pendente agendada">○ Agendada</small>}
                    </td>
                    <td>
                      {t.origem} → {t.destino}
                    </td>
                    <td>{t.descricao}</td>
                    <td className="valor">{formatarReais(t.valor)}</td>
                    <td className="acoes">
                      <button className="perigo" onClick={() => excluirTransferencia(t)}>
                        Excluir
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <Cartoes contas={contas} atualizacao={atualizacao} aoAlterar={aoAlterar} />
    </>
  )
}

export default Contas
