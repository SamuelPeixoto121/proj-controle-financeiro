import { useState } from 'react'
import { calcularPontos, ultimoMesRealizado } from './escalaAcumulado.js'
import { NOMES_MESES, formatarReais, mesAtual } from './formatar.js'

// Caminho SVG ligando os pontos: "M x0 y0 L x1 y1 L ..."
const caminho = (pontos) => pontos.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x} ${p.y}`).join(' ')

// Gráfico de linha: saldo acumulado no fim de cada mês do ano.
// Linha cheia = meses que já aconteceram; tracejada = previsão.
function GraficoAcumulado({ meses, ano }) {
  // Mês sob o mouse (ou tocado), para a linha vertical e a caixinha com o valor.
  const [ativo, setAtivo] = useState(null)

  const { pontos, yZero } = calcularPontos(meses.map((m) => m.acumulado))
  const ultimo = ultimoMesRealizado(ano, mesAtual())
  const realizados = pontos.slice(0, ultimo + 1)
  const previsao = pontos.slice(Math.max(ultimo, 0)) // começa no último realizado, para as linhas se ligarem
  const fim = pontos[pontos.length - 1]
  // O rótulo do último valor fica do lado oposto ao de onde a linha chega:
  // acima do ponto se a linha sobe (ou fica reta), abaixo se ela desce.
  const subindo = fim.valor >= pontos[pontos.length - 2].valor
  const marcado = ativo === null ? null : pontos[ativo]

  // A caixinha fica acima do ponto (ou abaixo, se o ponto estiver lá no alto)
  // e encostada na borda nos meses das pontas, para não sair do card.
  const dicaX = ativo < 3 ? '-12px' : ativo > 8 ? 'calc(-100% + 12px)' : '-50%'
  const dicaY = marcado && marcado.y > 40 ? 'calc(-100% - 12px)' : '12px'

  return (
    <div className="acumulado">
      <h3>Evolução do saldo acumulado</h3>
      {ultimo >= 0 && ultimo < 11 && (
        <p className="legenda-linha">
          <span className="traco" /> realizado <span className="traco tracejado" /> previsão
        </p>
      )}

      <div
        className="acumulado-area"
        role="img"
        aria-label={`Saldo acumulado no fim de cada mês de ${ano}. Em dezembro: ${formatarReais(fim.valor)}.`}
        onMouseLeave={() => setAtivo(null)}
      >
        <svg viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
          <line className="acumulado-zero" x1="0" x2="100" y1={yZero} y2={yZero} />
          {marcado && <line className="acumulado-cursor" x1={marcado.x} x2={marcado.x} y1="0" y2="100" />}
          {realizados.length > 1 && <path className="acumulado-linha" d={caminho(realizados)} />}
          {previsao.length > 1 && <path className="acumulado-linha previsao" d={caminho(previsao)} />}
        </svg>

        <span className="acumulado-rotulo-zero" style={{ top: `${yZero}%` }}>
          R$ 0
        </span>

        {/* Bolinha e valor do último mês (único número escrito no gráfico) */}
        <span className="acumulado-ponto" style={{ left: `${fim.x}%`, top: `${fim.y}%` }} />
        {ativo !== 11 && (
          <span
            className="acumulado-valor-final"
            style={{ top: `${fim.y}%`, transform: subindo ? 'translateY(-170%)' : 'translateY(70%)' }}
          >
            {formatarReais(fim.valor)}
          </span>
        )}

        {marcado && (
          <>
            <span className="acumulado-ponto" style={{ left: `${marcado.x}%`, top: `${marcado.y}%` }} />
            <span
              className="acumulado-dica"
              style={{ left: `${marcado.x}%`, top: `${marcado.y}%`, transform: `translate(${dicaX}, ${dicaY})` }}
            >
              <strong>
                {NOMES_MESES[ativo]}/{ano}
              </strong>{' '}
              {formatarReais(marcado.valor)}
              {ativo > ultimo && <small> (previsão)</small>}
            </span>
          </>
        )}

        {/* Áreas invisíveis, uma por mês, maiores que a linha: facilitam acertar com o mouse ou o dedo */}
        <div className="acumulado-alvos">
          {pontos.map((_, i) => (
            <span key={i} onMouseEnter={() => setAtivo(i)} onClick={() => setAtivo(i)} />
          ))}
        </div>
      </div>

      <div className="rotulos-meses" aria-hidden="true">
        {NOMES_MESES.map((nome) => (
          <span key={nome}>{nome}</span>
        ))}
      </div>
    </div>
  )
}

export default GraficoAcumulado
