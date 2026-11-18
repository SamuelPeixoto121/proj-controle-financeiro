import { useEffect, useState } from 'react'

const API_URL = 'http://localhost:5083'

const formatarReais = (valor) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

function App() {
  const [saldo, setSaldo] = useState(null)
  const [erro, setErro] = useState('')

  useEffect(() => {
    fetch(`${API_URL}/saldo`)
      .then((resposta) => resposta.json())
      .then(setSaldo)
      .catch(() => setErro('Não foi possível conectar à API. Ela está rodando?'))
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
    </main>
  )
}

export default App
