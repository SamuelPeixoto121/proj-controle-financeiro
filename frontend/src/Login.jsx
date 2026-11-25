import { useEffect, useState } from 'react'
import { api } from './api.js'
import BotaoTema from './BotaoTema.jsx'

// Tela de entrada: serve para entrar e para criar conta.
function Login({ aoEntrar }) {
  const [criandoConta, setCriandoConta] = useState(false)
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')
  const [enviando, setEnviando] = useState(false)
  // Site de demonstração? (undefined = ainda perguntando à API)
  const [demo, setDemo] = useState(undefined)

  useEffect(() => {
    api('/auth/modo')
      .then((modo) => setDemo(modo.demo))
      .catch(() => setDemo(false))
  }, [])

  async function entrarComoVisitante() {
    setErro('')
    setEnviando(true)
    try {
      aoEntrar(await api('/auth/visitante', { method: 'POST' }))
    } catch (e) {
      setErro(e.message)
      setEnviando(false)
    }
  }

  async function enviar(evento) {
    evento.preventDefault()
    setErro('')
    setEnviando(true)
    try {
      const rota = criandoConta ? '/auth/cadastro' : '/auth/login'
      aoEntrar(await api(rota, { method: 'POST', body: { email, senha } }))
    } catch (e) {
      setErro(e.message)
    } finally {
      setEnviando(false)
    }
  }

  function trocarModo() {
    setCriandoConta(!criandoConta)
    setErro('')
  }

  const topo = (
    <div className="login-topo">
      <h1>💰 Controle Financeiro</h1>
      <BotaoTema />
    </div>
  )

  if (demo === undefined)
    return (
      <main className="login card">
        {topo}
        <p className="carregando">Carregando...</p>
      </main>
    )

  // Site de demonstração: sem cadastro, só o botão de visitante.
  if (demo)
    return (
      <main className="login card">
        {topo}
        <div className="formulario-vertical login-demo">
          <p>
            Este é um <strong>site de demonstração</strong> de um app de controle financeiro pessoal
            (React + ASP.NET Core + MySQL).
          </p>
          <p>
            Entre como visitante para explorar com <strong>dados inventados</strong>: um ano e pouco de receitas e
            despesas, cartão de crédito, contas, metas e relatórios. A sua conta de visitante é só sua e é apagada
            em 24 horas.
          </p>
          {erro && <p className="erro">{erro}</p>}
          <button type="button" className="principal" onClick={entrarComoVisitante} disabled={enviando}>
            {enviando ? 'Preparando os dados...' : 'Entrar como visitante'}
          </button>
        </div>
      </main>
    )

  return (
    <main className="login card">
      {topo}
      <h2>{criandoConta ? 'Criar conta' : 'Entrar'}</h2>

      <form onSubmit={enviar} className="formulario-vertical">
        <label>
          E-mail
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            required
          />
        </label>
        <label>
          Senha
          <input
            type="password"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            autoComplete={criandoConta ? 'new-password' : 'current-password'}
            minLength={criandoConta ? 6 : undefined}
            required
          />
        </label>
        {criandoConta && <small>A senha deve ter pelo menos 6 caracteres.</small>}

        {erro && <p className="erro">{erro}</p>}

        <button type="submit" className="principal" disabled={enviando}>
          {enviando ? 'Aguarde...' : criandoConta ? 'Criar conta' : 'Entrar'}
        </button>
      </form>

      <p>
        {criandoConta ? 'Já tem conta?' : 'Ainda não tem conta?'}{' '}
        <button type="button" className="link" onClick={trocarModo}>
          {criandoConta ? 'Entrar' : 'Criar conta'}
        </button>
      </p>
    </main>
  )
}

export default Login
