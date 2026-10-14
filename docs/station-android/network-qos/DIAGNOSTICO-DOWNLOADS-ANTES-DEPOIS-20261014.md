# Downloads Station: comparação antes/depois da divisão de banda

Diagnóstico documental de 14/10/2026. Esta análise não altera configuração, serviço, APK ou download em andamento. O ajuste operacional posterior está registrado separadamente no recibo do servidor.

## Evidências em ordem cronológica

| Momento (UTC) | Evidência | Limite da conclusão |
| --- | --- | --- |
| 13/10, 19:50–19:51 | No moto g52 com R141, o GET de artefato transferiu 546.776.387 bytes em 75,288 s: **7,26 MB/s decimais de média**. Autorização e artefato responderam 200. | Um download Android real, sem teste pareado da mesma rede/arquivo. Fonte: `TurboElden/docs/server/r141/evidence/android-download-moto-g52.json`. |
| 13/10, 20:04 | O operador relatou ganho superior ao dobro e pico próximo de **10 MB/s** na candidata histórica de download direto. | Relato, sem SHA instalado conferido por ADB ou comparação controlada. Fonte: `TurboElden/docs/server/r147/evidence/historical-direct-download-branch-audit.json`. |
| 13/10, 22:20 | A política de QoS passou a limitar a **saída WAN agregada a 33 Mb/s** na interface de 95 Mb/s; reserva bulk/download de 60%, com empréstimo até o teto WAN. | Aplicação documentada em [server-return.json](server-return.json); não é medida atual de um telefone. |
| 14/10 | O operador relata agora **3–4 MB/s** nos downloads. | Taxa informada, ainda sem bytes/tempo e rota WAN/LAN medidos nesta tentativa. |
| 14/10, 12:15 | No A56 com a R148 instalada, a tela de The Wind Waker indicava download em 31% e **3,08 MB/s**. | Leitura pontual do indicador do aplicativo por USB, sem bytes/duração do GET nem rota WAN/LAN confirmados; o download não foi interrompido. |
| 14/10, 12:31 | O servidor elevou o teto de saída de **33 para 100 Mb/s**; a WAN passou a ter garantia de 90 Mb/s, com empréstimo até 100 Mb/s. | Aplicação comprovada em [limits-20261014.json](limits-20261014.json); taxa do provedor e download Android após o ajuste ainda não foram medidos. |

**Cálculo para o período anterior às 12:31:** 33 Mb/s ÷ 8 = **4,125 MB/s decimais** de teto teórico compartilhado para a WAN; cabeçalhos e tráfego concorrente reduzem a taxa útil. A classe de downloads tinha reserva de 19,8 Mb/s (2,475 MB/s), com empréstimo até os 33 Mb/s. A faixa de 3–4 MB/s observada antes do ajuste é **compatível** com essa política se o telefone recebia pela WAN. A coincidência temporal e numérica faz do limite anterior a hipótese principal para aquela queda, sem provar a rota real nem excluir Wi-Fi, operadora, concorrência ou arquivo.

Na configuração anterior, [`policy.py`](../../../ops/station-network-qos/policy.py) classificava a saída HTTPS (`tcp sport 443`) como bulk, salvo o fluxo de partida marcado, sob o teto WAN de 33 Mb/s. O novo recibo indica teto configurado de **100 Mb/s** (máximo teórico agregado de **12,5 MB/s**), sem comprovar essa taxa útil no telefone. O enlace físico continua em 100 Mb/s e a medição anterior de upload foi 36,940 Mb/s no Ookla e aproximadamente 29 Mb/s no destino Cloudflare; a comparação Ookla posterior não confirmou o mesmo servidor. O novo valor veio do plano informado pelo operador, **sem medição nova do provedor**. Se 3–4 MB/s persistirem após 12:31, o teto antigo de 33 Mb/s já não explica sozinho a taxa: medir caminho, capacidade real e tráfego concorrente antes de atribuir a causa ao app ou propor novo ajuste.

## Contrato do aplicativo

A R141 mudou **somente** o GET canônico `/v1/station/artifacts/{grantId}` para `https://downloads-station.lzgames.com.br`; autorização, catálogo, login e demais chamadas mantiveram a origem anterior. R143, R145, R146 e R147 documentam a preservação desse caminho e dos parâmetros de transferência. A R146 acrescentou no mesmo host uma rota **WSS v3 de partidas**, `/v1/station/online/multiplayer/relay`, com fábrica TLS própria; isso não substitui nem acelera o GET de artefato. A R148 alterou somente a apresentação das sinopses dos sistemas e preservou a rota de download e os DEX da R147. A R149 funcional substitui DEX10 e identidades vinculadas; as outras 13.855 entradas, incluindo downloads, foram preservadas. Fontes: `TurboElden/docs/server/r141/README.md`, `r145/README.md`, `r146/README.md`, `r147/README.md`, `r148/README.md` e [handoff R149](../r149/README.md). Não há regressão de download no app comprovada por esses recibos. A R148 está instalada no A56 com SHA conferido; a observação de 3,08 MB/s ainda não mede o caminho ou o limite efetivo da rede.

## Medir antes de decidir qualquer ajuste

1. No servidor, registrar em modo somente leitura o estado **atual** da política (`policy.py status`), taxa WAN, velocidade negociada da interface e contadores das classes WAN/bulk; confirmar que a configuração aplicada ainda corresponde ao recibo. Não executar novamente `deploy.py`.
2. No telefone, identificar se a transferência chega pela WAN ou LAN e registrar aparelho, APK efetivamente instalado, rede, título/artefato, horário, bytes do corpo e duração do GET. Não usar o contador de instalação/expansão como bytes de rede. Não interromper o download atual para fazer essa coleta.
3. Para comparação, usar o mesmo aparelho, arquivo e condições de rede, com **nova autorização/grant a cada tentativa** (grant de uso único). Medir uma tentativa isolada e outra com carga representativa, junto com perda/latência de uma partida. Não reutilizar grant nem remover validação, pin TLS ou limites do app.
4. Medir a capacidade de upload sustentada do servidor para o **mesmo caminho de rede** do cliente e conferir cabo/porta que mantêm o enlace a 100 Mb/s. Só então avaliar se há margem para outro teto WAN, preservando prioridade e reservas de jogo/serviços/download.

O ajuste para 100 Mb/s já consta do recibo externo; **não o repetir nem elevar mais por hipótese**. Qualquer revisão futura precisa de medições diretas, valor justificado, backup/retorno e verificação de taxa, perdas e latência conforme [README.md](README.md). Este documento não executa mudança operacional. A rota GET da R141 permanece nas candidatas atuais, e não há evidência que peça recompilação do app para tratar a taxa após esse ajuste.
