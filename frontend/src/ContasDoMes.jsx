import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarData, formatarReais } from './formatar.js'

// Contas pendentes do mês (e as atrasadas de antes), com botão para marcar como paga/recebida.
function ContasDoMes({ mes, atualizacao, aoAlterar }) {
  const [dados, setDados] = useState(null)
  const [erro, setErro] = useState('')

  useEffect(() => {
    api(mes ? `/pendentes?mes=${mes}` : '/pendentes')
      .then(setDados)
      .catch((e) => setErro(e.message))
  }, [mes, atualizacao])

  async function marcar(transacao) {
    setErro('')
    try {
      await api(`/transacoes/${transacao.id}/pago`, { method: 'PUT', body: { pago: true } })
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
      {dados && dados.transacoes.length === 0 && <p className="vazio">Tudo em dia ✔</p>}

      {dados && dados.transacoes.length > 0 && (
        <ul className="contas">
          {dados.transacoes.map((t) => (
            <li key={t.id} className={t.atrasada ? 'atrasada' : ''}>
              <span className="conta-data">
                {t.atrasada ? '⚠' : '○'} {formatarData(t.data).slice(0, 5)}
              </span>
              <span className="conta-descricao">
                {t.descricao}
                {t.serieId && <span className="serie"> ↻</span>}
                <small className="texto-suave">
                  {' '}
                  {t.categoria} · {t.tipo === 'Receita' ? 'a receber' : 'a pagar'}
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
