// Contas usadas no relatório para imprimir (separadas para dar para testar).

// "2026-11" -> "novembro de 2026"
const MESES_POR_EXTENSO = [
  'janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho',
  'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro',
]
export const mesPorExtenso = (mes) => `${MESES_POR_EXTENSO[Number(mes.slice(5, 7)) - 1]} de ${mes.slice(0, 4)}`

// Quanto um valor é do total, em %, com uma casa decimal (total zero = 0%).
export const porcentagemDe = (valor, total) => (total ? Math.round((valor / total) * 1000) / 10 : 0)

// Só as transações do ano, da mais antiga para a mais nova.
export const transacoesDoAno = (transacoes, ano) =>
  transacoes
    .filter((t) => t.data.startsWith(`${ano}-`))
    .sort((a, b) => a.data.localeCompare(b.data) || a.id - b.id)

// Despesas somadas por categoria, da maior para a menor, com a % de cada uma no total.
export function despesasPorCategoria(transacoes) {
  const totais = new Map()
  for (const t of transacoes) {
    if (t.tipo !== 'Despesa') continue
    const nome = t.categoria?.nome ?? 'Sem categoria'
    totais.set(nome, Math.round(((totais.get(nome) ?? 0) + t.valor) * 100) / 100)
  }
  const total = [...totais.values()].reduce((soma, v) => soma + v, 0)
  return [...totais]
    .map(([categoria, valor]) => ({ categoria, valor, porcentagem: porcentagemDe(valor, total) }))
    .sort((a, b) => b.valor - a.valor || a.categoria.localeCompare(b.categoria))
}

// Total de receitas e de despesas de uma lista de transações.
export function totaisDe(transacoes) {
  const soma = (tipo) =>
    Math.round(transacoes.filter((t) => t.tipo === tipo).reduce((s, t) => s + t.valor, 0) * 100) / 100
  return { receitas: soma('Receita'), despesas: soma('Despesa') }
}
