import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarReais, mesAtual } from './formatar.js'
import Categorias from './Categorias.jsx'
import DespesasPorCategoria from './DespesasPorCategoria.jsx'
import Transacoes from './Transacoes.jsx'

function App() {
  const [saldo, setSaldo] = useState(null)
  const [erro, setErro] = useState('')
  // Sobe a cada alteração, para o saldo e as listas recarregarem.
  const [atualizacao, setAtualizacao] = useState(0)
  const aoAlterar = () => setAtualizacao((n) => n + 1)
  // Mês escolhido ("2026-11"). Vazio = todos os meses.
  const [mes, setMes] = useState(mesAtual)

  useEffect(() => {
    api(mes ? `/saldo?mes=${mes}` : '/saldo')
      .then(setSaldo)
      .catch((e) => setErro(e.message))
  }, [atualizacao, mes])

  return (
    <main>
      <h1>Controle Financeiro</h1>

      <div className="linha filtro">
        <label htmlFor="mes">Mês:</label>
        <input id="mes" type="month" value={mes} onChange={(e) => setMes(e.target.value)} />
        <button onClick={() => setMes('')} disabled={!mes}>
          Todos
        </button>
        {!mes && <span>Mostrando todos os meses</span>}
      </div>

      {erro && <p className="erro">{erro}</p>}
      {!erro && !saldo && <p>Carregando...</p>}

      {saldo && (
        <ul className="resumo">
          <li>
            Receitas: <span className="receita">{formatarReais(saldo.receitas)}</span>
          </li>
          <li>
            Despesas: <span className="despesa">{formatarReais(saldo.despesas)}</span>
          </li>
          <li>
            <strong>
              Saldo:{' '}
              <span className={saldo.saldo < 0 ? 'despesa' : 'receita'}>
                {formatarReais(saldo.saldo)}
              </span>
            </strong>
          </li>
        </ul>
      )}

      <DespesasPorCategoria mes={mes} atualizacao={atualizacao} />
      <Transacoes mes={mes} atualizacao={atualizacao} aoAlterar={aoAlterar} />
      <Categorias aoAlterar={aoAlterar} />
    </main>
  )
}

export default App
