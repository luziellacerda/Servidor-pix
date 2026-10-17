# R164 — entrega delimitada da saída do anfitrião

**Estado:** código e testes preparados; implantação e prova física pendentes. Esta entrega parte do retorno R164 `346a749` e não altera o kit, o APK, os quatro motores, perfis, modos, catálogo ou capas.

## Falha observada

Após a saída humana do anfitrião N64, o convidado já permaneceu em tela preta/espera sem aviso. No servidor anterior, `RetireRoom` marcava o stream terminal e cancelava imediatamente o token compartilhado com o escritor WebSocket. Assim, o convidado podia receber apenas o fechamento da conexão, sem o frame terminal `STATE=3`. Um frame de jogo ainda em trânsito também podia fazer o leitor cancelar o escritor antes da entrega desse estado.

## Correção de servidor

- Somente uma saída **confirmada** pelo endpoint `leave` dá ao escritor uma janela limitada de até 2 segundos para enviar `STATE=3` antes do fechamento.
- O leitor respeita a mesma janela quando recebe um frame em trânsito após a confirmação. A reserva do stream é liberada depois da desconexão.
- Perda de rede sem `leave` confirmado continua recuperável; erros nativos e de protocolo não ganham a janela de saída humana.
- Os recibos idempotentes, autenticação, sala, modos, controles e políticas de admissão permanecem iguais.

Commits locais: `afc7b81076ed31b6286e95397a55ef5f4cf26c8e`, `84e330b67d2d0cd58ecb7bf716c6fb7d00174241` e `a7eed887bbb39abf6bb9ac051dc55026a3487b67`.

## Verificação realizada

`StationMultiplayer.Tests.csproj` passou **1.466 verificações**. O teste determinístico mantém um frame de jogo em trânsito e bloqueia o envio terminal; depois de `leave`, comprova que a escrita não é cancelada, exige a ordem `STATE=3` → fechamento e confere a liberação da reserva. `StationMultiplayer.Http.Tests.csproj` passou **226 verificações**, incluindo WSS/TLS com tráfego durante a saída e fechamento limitado. Esses testes não representam dois Android físicos.

## Aplicação e aceite

O operador deve comparar o código com a baseline R164, compilar e testar o servidor pelo fluxo vigente, preservar backup/rollback e implantar somente os binários correspondentes a esta alteração. **Não repetir cadastro de motores, publicação da capa 1080, importação de jogos ou transações R161/R162/R164.** Registrar commit, hash do binário instalado, horário, PID/reinício e resultado do teste. Nenhuma implantação é afirmada por este documento.

No teste físico, usar a mesma R164 e a mesma edição N64 em dois aparelhos. Registrar no convidado `STATE=3` ou fechamento de socket, aviso na tela e retorno à sala durante pelo menos 25 segundos depois de o anfitrião sair. Registrar a confirmação do `leave` no anfitrião sem publicar tokens, IDs de sala ou logs brutos. A correção de servidor cobre o `leave` confirmado; uma falha de transporte antes da confirmação ainda requer repetição limitada no app com o mesmo `requestId` e conferência de sala/geração/identidade.
