# Downloads Station: comparação antes/depois da divisão de banda

Diagnóstico documental de 14/10/2026. Nenhuma configuração, serviço, APK ou download em andamento foi alterado nesta análise.

## Evidências em ordem cronológica

| Momento (UTC) | Evidência | Limite da conclusão |
| --- | --- | --- |
| 13/10, 19:50–19:51 | No moto g52 com R141, o GET de artefato transferiu 546.776.387 bytes em 75,288 s: **7,26 MB/s decimais de média**. Autorização e artefato responderam 200. | Um download Android real, sem teste pareado da mesma rede/arquivo. Fonte: `TurboElden/docs/server/r141/evidence/android-download-moto-g52.json`. |
| 13/10, 20:04 | O operador relatou ganho superior ao dobro e pico próximo de **10 MB/s** na candidata histórica de download direto. | Relato, sem SHA instalado conferido por ADB ou comparação controlada. Fonte: `TurboElden/docs/server/r147/evidence/historical-direct-download-branch-audit.json`. |
| 13/10, 22:20 | A política de QoS passou a limitar a **saída WAN agregada a 33 Mb/s** na interface de 95 Mb/s; reserva bulk/download de 60%, com empréstimo até o teto WAN. | Aplicação documentada em [server-return.json](server-return.json); não é medida atual de um telefone. |
| 14/10 | O operador relata agora **3–4 MB/s** nos downloads. | Taxa informada, ainda sem bytes/tempo e rota WAN/LAN medidos nesta tentativa. |

**Cálculo:** 33 Mb/s ÷ 8 = **4,125 MB/s decimais** de teto teórico compartilhado para a WAN; cabeçalhos e tráfego concorrente reduzem a taxa útil. A classe de downloads tem reserva de 19,8 Mb/s (2,475 MB/s), mas pode tomar capacidade ociosa até os 33 Mb/s. A faixa relatada de 3–4 MB/s é, portanto, **compatível** com a política se o telefone estiver recebendo pelo ramo WAN. A coincidência temporal e numérica torna o limite do servidor a hipótese principal; não comprova sozinho a rota real nem exclui variação de Wi-Fi, operadora, concorrência ou arquivo.

O código aplicado em [`policy.py`](../../../ops/station-network-qos/policy.py) cria uma classe WAN com `rate`/`ceil` de 33 Mb/s e classifica a saída HTTPS (`tcp sport 443`) como bulk, salvo o fluxo de partida marcado. Tráfego para a LAN usa ramo separado de até 95 Mb/s. O recibo mediu 36,940 Mb/s de upload antes da calibração e aproximadamente 29 Mb/s no destino Cloudflare, com enlace físico negociado em 100 Mb/s; a comparação Ookla posterior não confirmou o mesmo servidor de teste. Esses valores impedem concluir que elevar o teto para 80–100 Mb/s seria seguro ou sustentado.

## Contrato do aplicativo

A R141 mudou **somente** o GET canônico `/v1/station/artifacts/{grantId}` para `https://downloads-station.lzgames.com.br`; autorização, catálogo, login e demais chamadas mantiveram a origem anterior. R143, R145, R146 e a candidata R147 documentam a preservação desse caminho e dos parâmetros de transferência. A R146 acrescentou no mesmo host uma rota **WSS v3 de partidas**, `/v1/station/online/multiplayer/relay`, com fábrica TLS própria; isso não substitui nem acelera o GET de artefato. Fontes: `TurboElden/docs/server/r141/README.md`, `r145/README.md`, `r146/README.md` e `r147/README.md`. Não há regressão de download no app comprovada por esses recibos, e a R147 ainda não teve instalação física confirmada.

## Medir antes de decidir qualquer ajuste

1. No servidor, registrar em modo somente leitura o estado **atual** da política (`policy.py status`), taxa WAN, velocidade negociada da interface e contadores das classes WAN/bulk; confirmar que a configuração aplicada ainda corresponde ao recibo. Não executar novamente `deploy.py`.
2. No telefone, identificar se a transferência chega pela WAN ou LAN e registrar aparelho, APK efetivamente instalado, rede, título/artefato, horário, bytes do corpo e duração do GET. Não usar o contador de instalação/expansão como bytes de rede. Não interromper o download atual para fazer essa coleta.
3. Para comparação, usar o mesmo aparelho, arquivo e condições de rede, com **nova autorização/grant a cada tentativa** (grant de uso único). Medir uma tentativa isolada e outra com carga representativa, junto com perda/latência de uma partida. Não reutilizar grant nem remover validação, pin TLS ou limites do app.
4. Medir a capacidade de upload sustentada do servidor para o **mesmo caminho de rede** do cliente e conferir cabo/porta que mantêm o enlace a 100 Mb/s. Só então avaliar se há margem para outro teto WAN, preservando prioridade e reservas de jogo/serviços/download.

Se as medições confirmarem o teto como gargalo e capacidade real superior, o responsável pelo servidor deve preparar mudança **apenas na configuração protegida**, com valor justificado, backup/retorno e verificação de taxa, perdas e latência após recarregar somente o serviço de QoS conforme [README.md](README.md). Este documento não autoriza nem registra tal mudança. Não é necessária recompilação do aplicativo para alterar esse teto.
