import { useEffect, useState } from 'react'
import { api } from './api.js'
import { agruparFaturas, formatarData, formatarReais } from './formatar.js'

// Contas pendentes do mês (e as atrasadas de antes), com botão para marcar como paga/recebida.
function ContasDoMes({ mes, atualizacao, aoAlterar, saldoReal }) {
  const [dados, setDados] = useState(null)
  const [erro, setErro] = useState('')

  useEffect(() => {
    api(mes ? `/pendentes?mes=${mes}` : '/pendentes')
      .then(setDados)
      .catch((e) => setErro(e.message))
  }, [mes, atualizacao])

  // Uma conta comum, ou a fatura inteira de um cartão (todas as compras dela).
  async function marcar(conta) {
    setErro('')
    const caminho = conta.fatura
      ? `/cartoes/${conta.cartaoId}/faturas/${conta.data.slice(0, 10)}/pago`
      : `/transacoes/${conta.id}/pago`
    try {
      await api(caminho, { method: 'PUT', body: { pago: true } })
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    }
  }

  return (
    <section className="card">
      <div className="card-titulo">
        <h2>{mes ? 'Contas do mês' : 'Contas pendentes'}</h2>
        {dados && (
          <span className="texto-suave">
            A pagar: <strong className="despesa">{formatarReais(dados.aPagar)}</strong> · A receber:{' '}
            <strong className="receita">{formatarReais(dados.aReceber)}</strong>
          </span>
        )}
      </div>

      {erro && <p className="erro">{erro}</p>}

      {/* Como fica o saldo real quando tudo o que está pendente aqui for pago e recebido */}
      {dados && saldoReal !== undefined && dados.transacoes.length > 0 && (
        <p className="texto-suave depois-de-tudo">
          Depois de pagar e receber as contas abaixo, o saldo real fica em{' '}
          <strong className={saldoReal - dados.aPagar + dados.aReceber < 0 ? 'despesa' : 'receita'}>
            {formatarReais(saldoReal - dados.aPagar + dados.aReceber)}
          </strong>
        </p>
      )}
      {dados && dados.transacoes.length === 0 && <p className="vazio">Tudo em dia ✔</p>}

      {dados && dados.transacoes.length > 0 && (
        <ul className="contas">
          {agruparFaturas(dados.transacoes).map((t) => (
            <li key={t.fatura ? t.chave : t.id} className={t.atrasada ? 'atrasada' : ''}>
              <span className="conta-data">
                {t.atrasada ? '⚠' : '○'} {formatarData(t.data).slice(0, 5)}
              </span>
              <span className="conta-descricao">
                {t.fatura ? `💳 Fatura ${t.cartao}` : t.descricao}
                {t.serieId && <span className="serie"> ↻</span>}
                <small className="texto-suave">
                  {' '}
                  {t.fatura
                    ? `${t.compras} ${t.compras === 1 ? 'compra' : 'compras'} · a pagar`
                    : `${t.categoria} · ${t.tipo === 'Receita' ? 'a receber' : 'a pagar'}`}
                  {t.atrasada && <strong className="despesa"> · Atrasada</strong>}
                </small>
              </span>
              <span className={`valor ${t.tipo === 'Receita' ? 'receita' : 'despesa'}`}>
                {formatarReais(t.valor)}
              </span>
              <button onClick={() => marcar(t)}>
                ✔ {t.tipo === 'Receita' ? 'Recebida' : 'Paga'}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

export default ContasDoMes
