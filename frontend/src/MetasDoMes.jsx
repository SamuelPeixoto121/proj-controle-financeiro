import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarReais } from './formatar.js'

// Situação de cada meta: a cor nunca aparece sozinha, sempre com ícone e texto.
function situacao(porcentagem) {
  if (porcentagem > 100) return { classe: 'estourou', icone: '✖', texto: 'Estourou' }
  if (porcentagem >= 80) return { classe: 'perto', icone: '⚠', texto: 'Perto do limite' }
  return { classe: 'dentro', icone: '✔', texto: 'Dentro da meta' }
}

// Quanto já foi gasto do limite mensal de cada categoria.
function MetasDoMes({ mes, atualizacao }) {
  const [metas, setMetas] = useState([])
  const [erro, setErro] = useState('')

  useEffect(() => {
    if (!mes) return
    api(`/metas?mes=${mes}`)
      .then(setMetas)
      .catch((e) => setErro(e.message))
  }, [mes, atualizacao])

  return (
    <section>
      <h2>Metas do mês</h2>

      {!mes && <p>Escolha um mês para ver as metas.</p>}
      {mes && erro && <p className="erro">{erro}</p>}
      {mes && !erro && metas.length === 0 && (
        <p>Nenhuma meta definida. Para criar, clique em Editar numa categoria e preencha a meta mensal.</p>
      )}

      {mes && (
        <ul className="metas">
          {metas.map((m) => {
            const porcentagem = Math.round((m.gasto / m.meta) * 100)
            const s = situacao(porcentagem)
            return (
              <li key={m.categoriaId}>
                <span className="grafico-nome">{m.categoria}</span>
                <span className="meta-trilho" aria-hidden="true">
                  <span
                    className={`meta-barra ${s.classe}`}
                    style={{ width: `${Math.min(porcentagem, 100)}%` }}
                  />
                </span>
                <span className="meta-texto">
                  {formatarReais(m.gasto)} de {formatarReais(m.meta)} <small>({porcentagem}%)</small>
                  <br />
                  <small>
                    <span className={`meta-icone ${s.classe}`}>{s.icone}</span> {s.texto}
                  </small>
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}

export default MetasDoMes
