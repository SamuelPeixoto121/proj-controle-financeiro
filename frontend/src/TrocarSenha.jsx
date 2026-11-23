import { useState } from 'react'
import { api } from './api.js'

// Formulário de troca de senha (aparece embaixo do topo).
function TrocarSenha({ aoTrocar, aoCancelar }) {
  const [senhaAtual, setSenhaAtual] = useState('')
  const [novaSenha, setNovaSenha] = useState('')
  const [confirmacao, setConfirmacao] = useState('')
  const [erro, setErro] = useState('')
  const [enviando, setEnviando] = useState(false)

  async function salvar(evento) {
    evento.preventDefault()
    if (novaSenha !== confirmacao) {
      setErro('As senhas novas não são iguais.')
      return
    }
    setErro('')
    setEnviando(true)
    try {
      await api('/auth/trocar-senha', { method: 'POST', body: { senhaAtual, novaSenha } })
      aoTrocar()
    } catch (e) {
      setErro(e.message)
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form onSubmit={salvar} className="card formulario-vertical trocar-senha">
      <h2>Trocar senha</h2>
      <label>
        Senha atual
        <input
          type="password"
          value={senhaAtual}
          onChange={(e) => setSenhaAtual(e.target.value)}
          autoComplete="current-password"
          required
        />
      </label>
      <label>
        Nova senha
        <input
          type="password"
          value={novaSenha}
          onChange={(e) => setNovaSenha(e.target.value)}
          autoComplete="new-password"
          minLength={6}
          required
        />
      </label>
      <label>
        Confirmar nova senha
        <input
          type="password"
          value={confirmacao}
          onChange={(e) => setConfirmacao(e.target.value)}
          autoComplete="new-password"
          minLength={6}
          required
        />
      </label>
      <small>A nova senha deve ter pelo menos 6 caracteres.</small>

      {erro && <p className="erro">{erro}</p>}

      <div className="linha">
        <button type="submit" className="principal" disabled={enviando}>
          {enviando ? 'Aguarde...' : 'Salvar'}
        </button>
        <button type="button" onClick={aoCancelar}>
          Cancelar
        </button>
      </div>
    </form>
  )
}

export default TrocarSenha
