import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarReais, mesAtual } from './formatar.js'
import Categorias from './Categorias.jsx'
import DespesasPorCategoria from './DespesasPorCategoria.jsx'
import Login from './Login.jsx'
import MetasDoMes from './MetasDoMes.jsx'
import ResumoMensal from './ResumoMensal.jsx'
import Transacoes from './Transacoes.jsx'
import TrocarSenha from './TrocarSenha.jsx'

function App() {
  // undefined = ainda verificando, null = ninguém logado, objeto = usuário logado
  const [usuario, setUsuario] = useState(undefined)
  const [saldo, setSaldo] = useState(null)
  const [erro, setErro] = useState('')
  // Sobe a cada alteração, para o saldo e as listas recarregarem.
  const [atualizacao, setAtualizacao] = useState(0)
  const aoAlterar = () => setAtualizacao((n) => n + 1)
  // Mês escolhido ("2026-11"). Vazio = todos os meses.
  const [mes, setMes] = useState(mesAtual)

  // Ao abrir a página: já existe alguém logado?
  useEffect(() => {
    api('/auth/eu')
      .then(setUsuario)
      .catch(() => setUsuario(null))
  }, [])

  // Se a sessão expirar no meio do uso, volta para a tela de login.
  useEffect(() => {
    const voltarAoLogin = () => setUsuario(null)
    window.addEventListener('sessao-expirada', voltarAoLogin)
    return () => window.removeEventListener('sessao-expirada', voltarAoLogin)
  }, [])

  useEffect(() => {
    if (!usuario) return
    api(mes ? `/saldo?mes=${mes}` : '/saldo')
      .then(setSaldo)
      .catch((e) => setErro(e.message))
  }, [usuario, atualizacao, mes])

  // Formulário de troca de senha aberto? E o aviso "Senha alterada."
  const [trocandoSenha, setTrocandoSenha] = useState(false)
  const [aviso, setAviso] = useState('')

  async function sair() {
    await api('/auth/logout', { method: 'POST' }).catch(() => {})
    setSaldo(null)
    setErro('')
    setTrocandoSenha(false)
    setAviso('')
    setUsuario(null)
  }

  if (usuario === undefined) return <main>Carregando...</main>
  if (usuario === null) return <Login aoEntrar={setUsuario} />

  return (
    <main>
      <div className="linha topo">
        <h1>Controle Financeiro</h1>
        <span className="usuario">{usuario.email}</span>
        <button
          onClick={() => {
            setTrocandoSenha(!trocandoSenha)
            setAviso('')
          }}
        >
          Trocar senha
        </button>
        <button onClick={sair}>Sair</button>
      </div>

      {trocandoSenha && (
        <TrocarSenha
          aoTrocar={() => {
            setTrocandoSenha(false)
            setAviso('Senha alterada.')
          }}
          aoCancelar={() => setTrocandoSenha(false)}
        />
      )}
      {aviso && <p className="aviso">{aviso}</p>}

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

      <ResumoMensal mes={mes} atualizacao={atualizacao} />
      <MetasDoMes mes={mes} atualizacao={atualizacao} />
      <DespesasPorCategoria mes={mes} atualizacao={atualizacao} />
      <Transacoes mes={mes} atualizacao={atualizacao} aoAlterar={aoAlterar} />
      <Categorias aoAlterar={aoAlterar} />
    </main>
  )
}

export default App
