import { useEffect, useState } from 'react'
import { api } from './api.js'
import { NOMES_MESES, formatarReais, variacao } from './formatar.js'

// Texto e cor da variação. "subirEhBom": receitas e saldo subindo é bom; despesas, não.
function Variacao({ atual, anterior, subirEhBom, emReais = false, comparadoCom = '' }) {
  const sufixo = comparadoCom && ` vs ${comparadoCom}`
  let texto
  let diferenca = atual - anterior
  if (emReais) {
    texto = formatarReais(Math.abs(diferenca))
  } else {
    const pct = variacao(atual, anterior)
    if (pct === null) return <span className="texto-suave">{atual > 0 ? 'novo' : '—'}</span>
    diferenca = pct
    texto = `${Math.abs(pct)}%`
  }
  if (diferenca === 0) return <span className="texto-suave">igual{sufixo}</span>

  const subiu = diferenca > 0
  const bom = subiu === subirEhBom
  return (
    <span className={bom ? 'receita' : 'despesa'}>
      {subiu ? '▲' : '▼'} {texto}
      {sufixo}
    </span>
  )
}

// O ano escolhido comparado com o anterior: três números e as despesas por categoria.
function ComparacaoAnual({ ano, atualizacao }) {
  const [dados, setDados] = useState(null)
  const [erro, setErro] = useState('')

  useEffect(() => {
    api(`/resumo-anual?ano=${ano}`)
      .then(setDados)
      .catch((e) => setErro(e.message))
  }, [ano, atualizacao])

  if (erro) return <p className="erro">{erro}</p>
  // Espera chegar o ano certo (evita mostrar o ano anterior por um instante ao trocar).
  if (!dados || dados.ano !== ano) return null

  const { atual, anterior, categorias, ateMes } = dados
  const anoAnterior = ano - 1
  const vazio = (t) => t.receitas === 0 && t.despesas === 0
  // Ano sem nada: não há o que comparar (o resumo já avisa "Nenhuma transação").
  if (vazio(atual)) return null

  const numeros = [
    { titulo: 'Receitas', campo: 'receitas', subirEhBom: true },
    { titulo: 'Despesas', campo: 'despesas', subirEhBom: false },
    { titulo: 'Saldo do período', campo: 'saldo', subirEhBom: true, emReais: true },
  ]

  return (
    <div className="comparacao">
      <h3>
        {ano} comparado com {anoAnterior}
      </h3>
      {ateMes < 12 && (
        <p className="texto-suave">
          De jan a {NOMES_MESES[ateMes - 1]} nos dois anos, porque {ano} ainda não terminou.
        </p>
      )}

      <div className="comparacao-numeros">
        {numeros.map((n) => (
          <div key={n.campo}>
            <span className="cartao-titulo">{n.titulo}</span>
            <strong>{formatarReais(atual[n.campo])}</strong>
            <small>
              {vazio(anterior) ? (
                <span className="texto-suave">sem dados de {anoAnterior}</span>
              ) : (
                <Variacao
                  atual={atual[n.campo]}
                  anterior={anterior[n.campo]}
                  subirEhBom={n.subirEhBom}
                  emReais={n.emReais}
                  comparadoCom={anoAnterior}
                />
              )}
            </small>
          </div>
        ))}
      </div>

      {categorias.length > 0 && (
        <div className="tabela-rolagem">
          <table className="tabela-comparacao">
            <caption>Despesas por categoria (as que mais mudaram primeiro)</caption>
            <thead>
              <tr>
                <th>Categoria</th>
                <th className="valor">{ano}</th>
                <th className="valor">{anoAnterior}</th>
                <th className="valor">Variação</th>
              </tr>
            </thead>
            <tbody>
              {categorias.map((c) => (
                <tr key={c.categoriaId}>
                  <td>{c.categoria}</td>
                  <td className="valor">{c.atual ? formatarReais(c.atual) : '—'}</td>
                  <td className="valor">{c.anterior ? formatarReais(c.anterior) : '—'}</td>
                  <td className="valor">
                    <Variacao atual={c.atual} anterior={c.anterior} subirEhBom={false} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

export default ComparacaoAnual
