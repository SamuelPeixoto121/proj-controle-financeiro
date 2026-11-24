import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarData, formatarReais } from './formatar.js'

const cartaoVazio = { nome: '', diaFechamento: '', diaVencimento: '' }

// Situação da fatura: ícone + texto (não só cor).
const SITUACOES = {
  Paga: ['paga', '✔'],
  Aberta: ['pendente', '○'],
  Fechada: ['pendente', '●'],
  Atrasada: ['atrasada', '⚠'],
}

// Campos do cartão (usados para cadastrar e para editar).
function CamposCartao({ valores, aoMudar }) {
  const mudar = (campo) => (e) => aoMudar({ ...valores, [campo]: e.target.value })
  return (
    <>
      <label className="campo campo-largo">
        Nome
        <input value={valores.nome} onChange={mudar('nome')} placeholder="Ex.: Nubank" />
      </label>
      <label className="campo">
        Fecha dia
        <input type="number" min="1" max="28" value={valores.diaFechamento} onChange={mudar('diaFechamento')} />
      </label>
      <label className="campo">
        Vence dia
        <input type="number" min="1" max="28" value={valores.diaVencimento} onChange={mudar('diaVencimento')} />
      </label>
    </>
  )
}

const corpoDoCartao = (v) => ({
  nome: v.nome,
  diaFechamento: Number(v.diaFechamento),
  diaVencimento: Number(v.diaVencimento),
})

function Cartoes({ atualizacao, aoAlterar }) {
  const [cartoes, setCartoes] = useState([])
  const [novo, setNovo] = useState(cartaoVazio)
  const [editandoId, setEditandoId] = useState(null)
  const [editado, setEditado] = useState(cartaoVazio)
  const [erro, setErro] = useState('')
  // Faturas do cartão escolhido, e qual delas está na tela
  const [cartaoId, setCartaoId] = useState(null)
  const [faturas, setFaturas] = useState([])
  const [indice, setIndice] = useState(0)

  useEffect(() => {
    api('/cartoes')
      .then(setCartoes)
      .catch((e) => setErro(e.message))
  }, [atualizacao])

  // Sem cartão escolhido (ou se ele foi excluído), mostra o primeiro.
  const cartaoAtual = cartoes.find((c) => c.id === cartaoId) ?? cartoes[0]
  const idAtual = cartaoAtual?.id

  useEffect(() => {
    if (!idAtual) return
    api(`/cartoes/${idAtual}/faturas`)
      .then((lista) => {
        setFaturas(lista)
        // Começa na fatura mais antiga ainda não paga (ou na última, se todas foram pagas).
        const naoPaga = lista.findIndex((f) => f.situacao !== 'Paga')
        setIndice(naoPaga >= 0 ? naoPaga : lista.length - 1)
      })
      .catch((e) => setErro(e.message))
  }, [idAtual, atualizacao])

  // Executa uma ação na API, mostra o erro se houver e avisa o resto da tela.
  async function executar(acao) {
    setErro('')
    try {
      await acao()
      aoAlterar()
      return true
    } catch (e) {
      setErro(e.message)
      return false
    }
  }

  async function criar(evento) {
    evento.preventDefault()
    const ok = await executar(() => api('/cartoes', { method: 'POST', body: corpoDoCartao(novo) }))
    if (ok) setNovo(cartaoVazio)
  }

  async function salvarEdicao(evento) {
    evento.preventDefault()
    const ok = await executar(() =>
      api(`/cartoes/${editandoId}`, { method: 'PUT', body: corpoDoCartao(editado) }),
    )
    if (ok) setEditandoId(null)
  }

  async function excluir(cartao) {
    if (!confirm(`Excluir o cartão "${cartao.nome}"?`)) return
    await executar(() => api(`/cartoes/${cartao.id}`, { method: 'DELETE' }))
  }

  const fatura = faturas[indice]

  async function pagarFatura(pago) {
    await executar(() =>
      api(`/cartoes/${cartaoAtual.id}/faturas/${fatura.vencimento.slice(0, 10)}/pago`, {
        method: 'PUT',
        body: { pago },
      }),
    )
  }

  const [classe, icone] = fatura ? SITUACOES[fatura.situacao] : []

  return (
    <>
      <section className="card">
        <h2>Cartões de crédito</h2>
        <p className="texto-suave">
          As compras no cartão contam no mês em que a fatura vence. Compras no dia do fechamento ou depois
          entram na fatura seguinte.
        </p>

        <form onSubmit={criar} className="formulario">
          <CamposCartao valores={novo} aoMudar={setNovo} />
          <div className="formulario-botoes">
            <button type="submit" className="principal">
              Adicionar
            </button>
          </div>
        </form>

        {erro && <p className="erro">{erro}</p>}

        {cartoes.length === 0 && <p className="vazio">Nenhum cartão cadastrado.</p>}

        <ul className="lista">
          {cartoes.map((cartao) =>
            cartao.id === editandoId ? (
              <li key={cartao.id}>
                <form onSubmit={salvarEdicao} className="formulario">
                  <CamposCartao valores={editado} aoMudar={setEditado} />
                  <div className="formulario-botoes">
                    <button type="submit" className="principal">
                      Salvar
                    </button>
                    <button type="button" onClick={() => setEditandoId(null)}>
                      Cancelar
                    </button>
                  </div>
                  <small className="texto-suave">
                    Mudar os dias vale para as próximas compras; as já lançadas continuam nas faturas em que estão.
                  </small>
                </form>
              </li>
            ) : (
              <li key={cartao.id} className="linha">
                <span className="categoria-nome">
                  💳 {cartao.nome}
                  <small className="meta-info dias-cartao">
                    fecha {cartao.diaFechamento} · vence {cartao.diaVencimento}
                  </small>
                </span>
                <button
                  onClick={() => {
                    setEditandoId(cartao.id)
                    setEditado({ ...cartao })
                    setErro('')
                  }}
                >
                  Editar
                </button>
                <button className="perigo" onClick={() => excluir(cartao)}>
                  Excluir
                </button>
              </li>
            ),
          )}
        </ul>
      </section>

      {cartaoAtual && (
        <section className="card">
          <div className="card-titulo">
            <h2>Faturas</h2>
            {cartoes.length > 1 && (
              <select
                value={cartaoAtual.id}
                onChange={(e) => setCartaoId(Number(e.target.value))}
                aria-label="Cartão"
              >
                {cartoes.map((c) => (
                  <option key={c.id} value={c.id}>
                    💳 {c.nome}
                  </option>
                ))}
              </select>
            )}
          </div>

          {faturas.length === 0 && (
            <p className="vazio">
              Nenhuma compra no {cartaoAtual.nome}. Lance uma despesa na aba Transações escolhendo o cartão em
              “Pagamento”.
            </p>
          )}

          {fatura && (
            <>
              <div className="linha ano">
                <button onClick={() => setIndice(indice - 1)} disabled={indice === 0} aria-label="Fatura anterior">
                  ◀
                </button>
                <strong>Vence em {formatarData(fatura.vencimento)}</strong>
                <button
                  onClick={() => setIndice(indice + 1)}
                  disabled={indice === faturas.length - 1}
                  aria-label="Próxima fatura"
                >
                  ▶
                </button>
              </div>

              <div className="fatura-resumo">
                <div>
                  <span className="cartao-titulo">Total</span>
                  <strong className="cartao-valor despesa">{formatarReais(fatura.total)}</strong>
                  <small className="texto-suave">Fecha em {formatarData(fatura.fechamento)}</small>
                </div>
                <span className={`situacao ${classe}`}>
                  {icone} {fatura.situacao}
                </span>
                {fatura.situacao === 'Paga' ? (
                  <button onClick={() => pagarFatura(false)}>Desfazer pagamento</button>
                ) : (
                  <button className="principal" onClick={() => pagarFatura(true)}>
                    ✔ Pagar fatura
                  </button>
                )}
              </div>

              <div className="tabela-rolagem">
                <table className="tabela-fatura">
                  <thead>
                    <tr>
                      <th>Compra</th>
                      <th>Descrição</th>
                      <th>Categoria</th>
                      <th className="valor">Valor</th>
                    </tr>
                  </thead>
                  <tbody>
                    {fatura.compras.map((c) => (
                      <tr key={c.id}>
                        <td>{formatarData(c.dataCompra)}</td>
                        <td>{c.descricao}</td>
                        <td>{c.categoria}</td>
                        <td className="valor">{formatarReais(c.valor)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </section>
      )}
    </>
  )
}

export default Cartoes
