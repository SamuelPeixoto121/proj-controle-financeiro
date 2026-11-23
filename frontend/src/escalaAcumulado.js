// Posições (em %) dos pontos do gráfico de linha do saldo acumulado.
// x: centro de cada coluna de mês (alinha com os rótulos jan, fev...).
// y: 0% = topo, 100% = base. A escala sempre inclui o zero, para a linha do R$ 0 aparecer.
export function calcularPontos(valores) {
  const min = Math.min(0, ...valores)
  let max = Math.max(0, ...valores)
  if (max === min) max = min + 1 // todos iguais a zero: evita dividir por zero

  // Margens em cima e embaixo, para a bolinha e o rótulo do último valor caberem.
  const topo = 24
  const base = 86
  const y = (valor) => topo + ((max - valor) / (max - min)) * (base - topo)

  return {
    pontos: valores.map((valor, i) => ({ x: ((i + 0.5) / valores.length) * 100, y: y(valor), valor })),
    yZero: y(0),
  }
}

// Índice (0 a 11) do último mês que já aconteceu no ano do gráfico; os seguintes são previsão.
// Ano passado: 11 (tudo realizado). Ano futuro: -1 (tudo previsão).
export function ultimoMesRealizado(ano, mesDeHoje) {
  const anoAtual = Number(mesDeHoje.slice(0, 4))
  if (ano < anoAtual) return 11
  if (ano > anoAtual) return -1
  return Number(mesDeHoje.slice(5, 7)) - 1
}
