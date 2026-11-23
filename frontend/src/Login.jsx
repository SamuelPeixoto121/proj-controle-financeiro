import { useState } from 'react'
import { api } from './api.js'

// Tela de entrada: serve para entrar e para criar conta.
function Login({ aoEntrar }) {
  const [criandoConta, setCriandoConta] = useState(false)
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')
  const [enviando, setEnviando] = useState(false)

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

  return (
    <main className="login card">
      <h1>💰 Controle Financeiro</h1>
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
