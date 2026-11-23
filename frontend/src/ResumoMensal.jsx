import { useEffect, useState } from 'react'
import { api } from './api.js'
import { NOMES_MESES, formatarReais, mesAtual } from './formatar.js'

// Receitas e despesas de cada mês do ano: gráfico de colunas + tabela.
function ResumoMensal({ mes, atualizacao }) {
  // Começa no ano do mês escolhido no filtro (ou no ano atual).
  const [ano, setAno] = useState(() => Number((mes || mesAtual()).slice(0, 4)))
  const [meses, setMeses] = useState([])
  const [erro, setErro] = useState('')

  useEffect(() => {
    api(`/resumo-mensal?ano=${ano}`)
      .then(setMeses)
      .catch((e) => setErro(e.message))
  }, [ano, atualizacao])

  // A coluna mais alta do gráfico ocupa a altura toda.
  const maior = Math.max(0, ...meses.map((m) => Math.max(m.receitas, m.despesas)))
  const total = (campo) => meses.reduce((soma, m) => soma + m[campo], 0)
  const saldoTotal = total('saldo')
  // Quanto se tem no fim do ano (o acumulado de dezembro)
  const acumuladoFinal = meses.at(-1)?.acumulado ?? 0

  return (
    <section>
      <h2>Resumo mês a mês</h2>

      <div className="linha ano">
        <button onClick={() => setAno(ano - 1)} aria-label="Ano anterior">
          ◀
        </button>
        <strong>{ano}</strong>
        <button onClick={() => setAno(ano + 1)} aria-label="Próximo ano">
          ▶
        </button>
      </div>

      {erro && <p className="erro">{erro}</p>}
      {!erro && meses.length > 0 && maior === 0 && <p>Nenhuma transação em {ano}.</p>}

      {maior > 0 && (
        <>
          <ul className="legenda">
            <li>
              <span className="cor barra-receitas" /> Receitas
            </li>
            <li>
              <span className="cor barra-despesas" /> Despesas
            </li>
          </ul>

          <div className="colunas" aria-hidden="true">
            {meses.map((m, i) => (
              <div
                key={m.mes}
                className="coluna"
                title={`${NOMES_MESES[i]}/${ano}\nReceitas: ${formatarReais(m.receitas)}\nDespesas: ${formatarReais(m.despesas)}\nSaldo: ${formatarReais(m.saldo)}`}
              >
                <span className="barra barra-receitas" style={{ height: `${(m.receitas / maior) * 100}%` }} />
                <span className="barra barra-despesas" style={{ height: `${(m.despesas / maior) * 100}%` }} />
              </div>
            ))}
          </div>
          <div className="rotulos-meses" aria-hidden="true">
            {NOMES_MESES.map((nome) => (
              <span key={nome}>{nome}</span>
            ))}
          </div>

          <table>
            <thead>
              <tr>
                <th>Mês</th>
                <th className="valor">Receitas</th>
                <th className="valor">Despesas</th>
                <th className="valor">Saldo</th>
                <th className="valor">Acumulado</th>
              </tr>
            </thead>
            <tbody>
              {meses.map((m, i) => (
                <tr key={m.mes}>
                  <td>
                    {NOMES_MESES[i]}/{ano}
                  </td>
                  <td className="valor">{formatarReais(m.receitas)}</td>
                  <td className="valor">{formatarReais(m.despesas)}</td>
                  <td className={`valor ${m.saldo < 0 ? 'despesa' : 'receita'}`}>{formatarReais(m.saldo)}</td>
                  <td className={`valor ${m.acumulado < 0 ? 'despesa' : 'receita'}`}>
                    {formatarReais(m.acumulado)}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <th>Total</th>
                <th className="valor">{formatarReais(total('receitas'))}</th>
                <th className="valor">{formatarReais(total('despesas'))}</th>
                <th className={`valor ${saldoTotal < 0 ? 'despesa' : 'receita'}`}>{formatarReais(saldoTotal)}</th>
                <th className={`valor ${acumuladoFinal < 0 ? 'despesa' : 'receita'}`}>
                  {formatarReais(acumuladoFinal)}
                </th>
              </tr>
            </tfoot>
          </table>
        </>
      )}
    </section>
  )
}

export default ResumoMensal
