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
