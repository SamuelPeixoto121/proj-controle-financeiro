import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarReais } from './formatar.js'
import Categorias from './Categorias.jsx'
import Transacoes from './Transacoes.jsx'

function App() {
  const [saldo, setSaldo] = useState(null)
  const [erro, setErro] = useState('')
  // Sobe a cada alteração, para o saldo e as listas recarregarem.
  const [atualizacao, setAtualizacao] = useState(0)
  const aoAlterar = () => setAtualizacao((n) => n + 1)

  useEffect(() => {
    api('/saldo')
      .then(setSaldo)
      .catch((e) => setErro(e.message))
  }, [atualizacao])

  return (
    <main>
      <h1>Controle Financeiro</h1>

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

      <Transacoes atualizacao={atualizacao} aoAlterar={aoAlterar} />
      <Categorias aoAlterar={aoAlterar} />
    </main>
  )
}

export default App
