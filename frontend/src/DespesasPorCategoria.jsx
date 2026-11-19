import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarReais } from './formatar.js'

// Gráfico de barras horizontais: quanto se gastou em cada categoria.
function DespesasPorCategoria({ mes, atualizacao }) {
  const [totais, setTotais] = useState([])
  const [erro, setErro] = useState('')

  useEffect(() => {
    api(mes ? `/despesas-por-categoria?mes=${mes}` : '/despesas-por-categoria')
      .then(setTotais)
      .catch((e) => setErro(e.message))
  }, [mes, atualizacao])

  const totalGeral = totais.reduce((soma, item) => soma + item.total, 0)
  const maior = totais[0]?.total ?? 0 // a lista já vem do maior para o menor

  return (
    <section>
      <h2>Despesas por categoria</h2>

      {erro && <p className="erro">{erro}</p>}
      {!erro && totais.length === 0 && (
        <p>{mes ? 'Nenhuma despesa neste mês.' : 'Nenhuma despesa cadastrada.'}</p>
      )}

      <ul className="grafico">
        {totais.map((item) => {
          const porcentagem = Math.round((item.total / totalGeral) * 100)
          return (
            <li
              key={item.categoriaId}
              title={`${item.categoria}: ${formatarReais(item.total)} (${porcentagem}% das despesas)`}
            >
              <span className="grafico-nome">{item.categoria}</span>
              <span className="grafico-trilho" aria-hidden="true">
                <span
                  className="grafico-barra"
                  style={{ width: `${(item.total / maior) * 100}%` }}
                />
              </span>
              <span className="grafico-valor">
                {formatarReais(item.total)} <small>({porcentagem}%)</small>
              </span>
            </li>
          )
        })}
      </ul>
    </section>
  )
}

export default DespesasPorCategoria
