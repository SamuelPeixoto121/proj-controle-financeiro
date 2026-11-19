import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarData, formatarReais, hoje } from './formatar.js'

const formularioVazio = () => ({
  descricao: '',
  valor: '',
  data: hoje(),
  tipo: 'Despesa',
  categoriaId: '',
})

function Transacoes({ mes, atualizacao, aoAlterar }) {
  const [transacoes, setTransacoes] = useState([])
  const [categorias, setCategorias] = useState([])
  const [formulario, setFormulario] = useState(formularioVazio)
  const [editandoId, setEditandoId] = useState(null)
  const [erro, setErro] = useState('')

  // Recarrega sempre que algo mudar em qualquer parte da tela ou o mês mudar.
  useEffect(() => {
    Promise.all([api(mes ? `/transacoes?mes=${mes}` : '/transacoes'), api('/categorias')])
      .then(([listaTransacoes, listaCategorias]) => {
        setTransacoes(listaTransacoes)
        setCategorias(listaCategorias)
      })
      .catch((e) => setErro(e.message))
  }, [mes, atualizacao])

  function alterarCampo(evento) {
    const { name, value } = evento.target
    setFormulario({ ...formulario, [name]: value })
  }

  function cancelarEdicao() {
    setEditandoId(null)
    setFormulario(formularioVazio())
    setErro('')
  }

  async function salvar(evento) {
    evento.preventDefault()
    setErro('')
    const corpo = {
      ...formulario,
      valor: Number(formulario.valor),
      categoriaId: Number(formulario.categoriaId),
    }
    try {
      if (editandoId) {
        await api(`/transacoes/${editandoId}`, { method: 'PUT', body: corpo })
      } else {
        await api('/transacoes', { method: 'POST', body: corpo })
      }
      cancelarEdicao()
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    }
  }

  function editar(transacao) {
    setEditandoId(transacao.id)
    setFormulario({
      descricao: transacao.descricao,
      valor: String(transacao.valor),
      data: transacao.data.slice(0, 10),
      tipo: transacao.tipo,
      categoriaId: String(transacao.categoriaId),
    })
    setErro('')
  }

  async function excluir(transacao) {
    if (!confirm(`Excluir a transação "${transacao.descricao}"?`)) return
    setErro('')
    try {
      await api(`/transacoes/${transacao.id}`, { method: 'DELETE' })
      if (transacao.id === editandoId) cancelarEdicao()
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    }
  }

  return (
    <section>
      <h2>Transações</h2>

      <form onSubmit={salvar} className="formulario">
        <input
          name="descricao"
          value={formulario.descricao}
          onChange={alterarCampo}
          placeholder="Descrição"
        />
        <input
          name="valor"
          type="number"
          step="0.01"
          min="0.01"
          value={formulario.valor}
          onChange={alterarCampo}
          placeholder="Valor"
        />
        <input name="data" type="date" value={formulario.data} onChange={alterarCampo} />
        <select name="tipo" value={formulario.tipo} onChange={alterarCampo}>
          <option value="Despesa">Despesa</option>
          <option value="Receita">Receita</option>
        </select>
        <select name="categoriaId" value={formulario.categoriaId} onChange={alterarCampo}>
          <option value="">Categoria...</option>
          {categorias.map((categoria) => (
            <option key={categoria.id} value={categoria.id}>
              {categoria.nome}
            </option>
          ))}
        </select>
        <button type="submit">{editandoId ? 'Salvar' : 'Adicionar'}</button>
        {editandoId && (
          <button type="button" onClick={cancelarEdicao}>
            Cancelar
          </button>
        )}
      </form>

      {erro && <p className="erro">{erro}</p>}

      {transacoes.length === 0 ? (
        <p>{mes ? 'Nenhuma transação neste mês.' : 'Nenhuma transação cadastrada.'}</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Data</th>
              <th>Descrição</th>
              <th>Categoria</th>
              <th>Tipo</th>
              <th className="valor">Valor</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {transacoes.map((transacao) => (
              <tr key={transacao.id}>
                <td>{formatarData(transacao.data)}</td>
                <td>{transacao.descricao}</td>
                <td>{transacao.categoria?.nome}</td>
                <td>{transacao.tipo}</td>
                <td className={`valor ${transacao.tipo === 'Receita' ? 'receita' : 'despesa'}`}>
                  {formatarReais(transacao.valor)}
                </td>
                <td className="acoes">
                  <button onClick={() => editar(transacao)}>Editar</button>
                  <button onClick={() => excluir(transacao)}>Excluir</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

export default Transacoes
