import { formatarReais, progressoEconomia } from './formatar.js'

// Situação da meta de economia: a cor nunca aparece sozinha, sempre com ícone e texto.
const SITUACOES = {
  batida: { classe: 'dentro', icone: '✔', texto: (v) => `Meta batida! Sobraram ${formatarReais(v)} além da meta.` },
  faltando: { classe: 'andamento', icone: '○', texto: (v) => `Faltam ${formatarReais(v)} para a meta.` },
  negativo: { classe: 'estourou', icone: '⚠', texto: (v) => `No vermelho: ${formatarReais(v)}.` },
}

// "Guardado: R$ X de R$ Y (Z%)", a barra e a situação. Usada no mês e no ano.
function BarraEconomia({ rotulo, saldo, meta }) {
  const p = progressoEconomia(saldo, meta)
  const s = SITUACOES[p.situacao]
  return (
    <div className="economia">
      <span className="meta-texto">
        {rotulo}: <strong>{formatarReais(saldo)}</strong> de {formatarReais(meta)} <small>({p.porcentagem}%)</small>
      </span>
      <span className="meta-trilho" aria-hidden="true">
        <span className={`meta-barra ${s.classe}`} style={{ width: `${p.barra}%` }} />
      </span>
      <small className="meta-situacao">
        <span className={`meta-icone ${s.classe}`}>{s.icone}</span> {s.texto(p.valor)}
      </small>
    </div>
  )
}

export default BarraEconomia
