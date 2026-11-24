import { useEffect, useState } from 'react'
import { api } from './api.js'
import BarraEconomia from './BarraEconomia.jsx'
import { formatarMes } from './formatar.js'

// Meta de economia do mês: quanto se quer guardar x o saldo do mês (receitas − despesas).
function MetaEconomia({ mes, saldoDoMes, atualizacao, aoAlterar }) {
  const [meta, setMeta] = useState(undefined) // undefined = carregando; null = sem meta
  const [editando, setEditando] = useState(false)
  const [valor, setValor] = useState('')
  const [erro, setErro] = useState('')

  useEffect(() => {
    api('/meta-economia')
      .then((r) => setMeta(r.metaMensal))
      .catch((e) => setErro(e.message))
  }, [atualizacao])

  async function salvar(novaMeta) {
    setErro('')
    try {
      const r = await api('/meta-economia', { method: 'PUT', body: { metaMensal: novaMeta } })
      setMeta(r.metaMensal)
      setEditando(false)
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    }
  }

  function comecarEdicao() {
    setValor(meta == null ? '' : String(meta))
    setEditando(true)
    setErro('')
  }

  if (meta === undefined && !erro) return null
  const mostrarFormulario = editando || meta == null

  return (
    <section className="card">
      <div className="card-titulo">
        <h2>Meta de economia</h2>
        {meta != null && !editando && <button onClick={comecarEdicao}>Alterar meta</button>}
      </div>

      {mostrarFormulario ? (
        <form
          className="linha meta-economia-form"
          onSubmit={(e) => {
            e.preventDefault()
            salvar(Number(valor))
          }}
        >
          <label htmlFor="meta-economia">Quanto você quer guardar por mês?</label>
          <input
            id="meta-economia"
            type="number"
            step="0.01"
            min="0.01"
            value={valor}
            onChange={(e) => setValor(e.target.value)}
            placeholder="Ex.: 1000,00"
            required
          />
          <button type="submit" className="principal">
            Salvar
          </button>
          {editando && meta != null && (
            <>
              <button type="button" onClick={() => setEditando(false)}>
                Cancelar
              </button>
              <button type="button" className="perigo" onClick={() => salvar(null)}>
                Remover meta
              </button>
            </>
          )}
        </form>
      ) : (
        <>
          <BarraEconomia rotulo={`Guardado em ${formatarMes(mes)}`} saldo={saldoDoMes} meta={meta} />
          <p className="texto-suave meta-economia-nota">
            Receitas − despesas do mês, contando o que ainda está pendente (previsão de como o mês vai fechar).
          </p>
        </>
      )}

      {erro && <p className="erro">{erro}</p>}
    </section>
  )
}

export default MetaEconomia
