// 3249.45 -> "R$ 3.249,45"
export const formatarReais = (valor) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

// "2026-11-05T00:00:00" -> "05/11/2026"
export const formatarData = (data) => data.slice(0, 10).split('-').reverse().join('/')

// Data de hoje no formato do <input type="date">: "2026-11-18".
// O formato sueco (sv-SE) já é AAAA-MM-DD e usa o fuso horário do computador.
export const hoje = () => new Date().toLocaleDateString('sv-SE')
