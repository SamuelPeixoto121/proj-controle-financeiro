// 3249.45 -> "R$ 3.249,45"
export const formatarReais = (valor) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

// "2026-11-05T00:00:00" -> "05/11/2026"
export const formatarData = (data) => data.slice(0, 10).split('-').reverse().join('/')

// Data de hoje no formato do <input type="date">: "2026-11-18".
// O formato sueco (sv-SE) já é AAAA-MM-DD e usa o fuso horário do computador.
export const hoje = () => new Date().toLocaleDateString('sv-SE')

// Mês atual no formato do <input type="month">: "2026-11".
export const mesAtual = () => hoje().slice(0, 7)

// Para buscas: "Salário" -> "salario" (sem acentos e em minúsculas).
// O NFD separa a letra do acento ("á" vira "a" + "´"), e o replace apaga os acentos.
export const normalizar = (texto) =>
  texto.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()

export const NOMES_MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

// "2026-11" -> "nov/2026"
export const formatarMes = (mes) => `${NOMES_MESES[Number(mes.slice(5, 7)) - 1]}/${mes.slice(0, 4)}`

// Prévia das parcelas, com a mesma conta da API (a 1ª fica com os centavos que sobram):
// (100, 3) -> "3x: a 1ª de R$ 33,34 e 2x de R$ 33,33"   (1200, 10) -> "10x de R$ 120,00"
export function previaDeParcelas(valorTotal, parcelas) {
  const centavos = Math.round(Number(valorTotal) * 100)
  const n = Number(parcelas)
  if (!(centavos > 0) || !Number.isInteger(n) || n < 2 || n > 60) return ''
  const base = Math.floor(centavos / n)
  if (base < 1) return 'Valor pequeno demais para tantas parcelas.'
  const primeira = centavos - base * (n - 1)
  const reais = (c) => formatarReais(c / 100)
  return primeira === base
    ? `${n}x de ${reais(base)}`
    : `${n}x: a 1ª de ${reais(primeira)} e ${n - 1}x de ${reais(base)}`
}
