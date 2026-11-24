import { describe, expect, it } from 'vitest'
import { calcularPontos, ultimoMesRealizado } from './escalaAcumulado.js'

// As margens do gráfico: o maior valor fica a 24% do topo, e o menor a 86%.
const TOPO = 24
const BASE = 86

describe('calcularPontos', () => {
  it('valores positivos: o zero na base e o maior no topo', () => {
    const { pontos, yZero } = calcularPontos([0, 0, 3170, 6079.6, 13780.9])
    expect(yZero).toBeCloseTo(BASE)
    expect(pontos[0].y).toBeCloseTo(BASE)
    expect(pontos[4].y).toBeCloseTo(TOPO)
    expect(pontos[3].valor).toBe(6079.6)
  })

  it('x no centro de cada coluna de mês', () => {
    const { pontos } = calcularPontos(new Array(12).fill(0))
    expect(pontos[0].x).toBeCloseTo(100 / 24)
    expect(pontos[11].x).toBeCloseTo(100 - 100 / 24)
  })

  it('com negativo: a linha do zero fica no meio da escala', () => {
    const { pontos, yZero } = calcularPontos([-500, 1500])
    expect(yZero).toBeCloseTo(TOPO + (1500 / 2000) * (BASE - TOPO))
    expect(pontos[0].y).toBeCloseTo(BASE)
    expect(pontos[1].y).toBeCloseTo(TOPO)
  })

  it('todos negativos: o zero no topo', () => {
    const { pontos, yZero } = calcularPontos([-100, -300, -200])
    expect(yZero).toBeCloseTo(TOPO)
    expect(pontos[1].y).toBeCloseTo(BASE)
  })

  it('todos zero: nada de NaN (não divide por zero)', () => {
    const { pontos, yZero } = calcularPontos([0, 0, 0])
    expect(pontos.every((p) => Number.isFinite(p.y))).toBe(true)
    expect(yZero).toBeCloseTo(BASE)
  })
})

describe('ultimoMesRealizado', () => {
  it.each([
    [2025, '2026-11', 11, 'ano passado: tudo realizado'],
    [2027, '2026-11', -1, 'ano futuro: tudo previsão'],
    [2026, '2026-11', 10, 'ano atual: até novembro'],
    [2026, '2026-01', 0, 'em janeiro: só janeiro'],
    [2026, '2026-12', 11, 'em dezembro: o ano todo'],
  ])('ano %i com hoje em %s = %i (%s)', (ano, hoje, esperado) => {
    expect(ultimoMesRealizado(ano, hoje)).toBe(esperado)
  })
})
