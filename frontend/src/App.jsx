import { useEffect, useState } from 'react'
import { api } from './api.js'
import Categorias from './Categorias.jsx'

const formatarReais = (valor) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

function App() {
  const [saldo, setSaldo] = useState(null)
  const [erro, setErro] = useState('')

  useEffect(() => {
    api('/saldo')
      .then(setSaldo)
      .catch((e) => setErro(e.message))
  }, [])

  return (
    <main>
      <h1>Controle Financeiro</h1>

      {erro && <p className="erro">{erro}</p>}
      {!erro && !saldo && <p>Carregando...</p>}

      {saldo && (
        <ul className="resumo">
          <li>Receitas: {formatarReais(saldo.receitas)}</li>
          <li>Despesas: {formatarReais(saldo.despesas)}</li>
          <li>
            <strong>Saldo: {formatarReais(saldo.saldo)}</strong>
          </li>
        </ul>
      )}

      <Categorias />
    </main>
  )
}

export default App
