# HANDOFF LINUX — aceitar PIX da versão Windows — rodada 10

Data: 2026-08-11  
Origem: Windows TurboRama

Status: **ENCERRADO COM RETORNO CONFIRMADO** em 2026-08-10. O servidor respondeu
`SERVIDOR_PRONTO_PARA_RECEBER_PIX: SIM`; não houve alteração no Linux.

Este é o **único handoff ativo** para a conversa Linux. Os handoffs das rodadas anteriores ficam
somente como histórico de auditoria; não combinar instruções antigas com esta rodada.

## Objetivo desta rodada

Confirmar que o servidor Linux já instalado está pronto para receber requisições PIX dos
programas Windows compilados. Esta rodada é de **aceitação e verificação**, não de atualização
do site, banco ou túnel.

## Regra de escopo

- Os arquivos `CONFIGURAR-USER-TOKEN-PIX.exe`, `CONFIGURAR-ACCESS-TOKEN-PIX.exe`,
  `TurboRamaPixAgent.exe` e `emulationstation.exe` são binários Windows para teste no gabinete.
- Eles **não devem ser copiados para o servidor Linux** e não fazem parte do pacote do serviço
  `turborama-pix`.
- O servidor deve continuar somente com a aplicação Linux já instalada em `/opt/turborama-pix`.
- Não alterar kiosk, EmulationStation, nginx, Cloudflare Tunnel/Access, MariaDB, firewall, NAT,
  roteador ou portas da empresa.
- Não solicitar, registrar ou transportar Access Token, Client Secret, senha ou código de ativação.
- Não criar licença, máquina, pagamento, order, QR ou cobrança real nesta rodada.

## Artefatos Windows disponíveis (somente referência para o teste do gabinete)

Os binários foram encontrados no Windows e passaram pelo autoteste local:

- `CONFIGURAR-USER-TOKEN-PIX.exe` — saída do autoteste: `0`;
- `CONFIGURAR-ACCESS-TOKEN-PIX.exe` — saída do autoteste: `0`;
- `TurboRamaPixAgent.exe` — saída do autoteste: `0`;
- `emulationstation.exe` — compilado e presente; não deve ser iniciado no servidor Linux.

Os caminhos e hashes desses arquivos ficam no Windows. Não copiar os executáveis para o Linux.

## Estado Linux que deve ser confirmado, sem pressupor

Confirmar somente por leitura:

1. O serviço `turborama-pix` está ativo e habilitado.
2. A aplicação atende localmente em `127.0.0.1:5187`.
3. `https://pix.lzgames.com.br/v1/health` retorna HTTP `200`.
4. O site principal continua funcionando.
5. O painel administrativo continua protegido pelo Cloudflare Access.
6. `pix.lzgames.com.br/admin*` continua isolado e não expõe o painel público da API.
7. nginx, cloudflared e MariaDB continuam ativos.
8. As portas protegidas da empresa continuam inalteradas e `5187` permanece somente em loopback.
9. As contagens de clientes, licenças, máquinas, pagamentos, credenciais e preços permanecem
   iguais às contagens observadas antes da rodada.

## Ação permitida

Se todas as verificações acima coincidirem com o estado anterior, não trocar nenhum arquivo e
não reiniciar nenhum serviço. Registrar o servidor como pronto para receber chamadas PIX do agente
Windows.

Só será permitida uma troca futura do pacote Linux se houver um ZIP de servidor novo, com hash e
autoteste comprovados em handoff separado. Os executáveis Windows desta rodada não autorizam uma
troca no Linux.

## Critérios de parada

Parar sem alteração se ocorrer qualquer divergência de serviço, health, rota, estado comercial,
porta, banco, site ou Cloudflare. Não tentar corrigir abrindo portas, alterando DNS, trocando
nginx ou recriando o túnel.

## Rollback

Como esta rodada não altera arquivos nem serviços, o rollback esperado é `NAO_APLICAVEL`.
Se alguém tiver alterado algo antes da auditoria, restaurar somente o backup já existente da
rodada anterior e registrar a divergência; não apagar backups.

## Retorno obrigatório

Criar no servidor:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-10.md`

O retorno deve conter apenas:

- data/hora e resultado: `SERVIDOR_PRONTO_PARA_RECEBER_PIX: SIM` ou `NAO`;
- códigos HTTP local/público e status dos serviços;
- confirmação de que não houve alteração em site, Cloudflare, nginx, MariaDB, firewall, NAT,
  roteador ou portas;
- contagens comerciais antes/depois;
- confirmação `LICENCA_CRIADA: NAO`, `COBRANCA_CRIADA: NAO`,
  `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- último passo concluído e qualquer divergência encontrada.

Não incluir segredos, cookies, tokens, senhas, códigos de ativação, conteúdo de `server.env` ou
headers de autorização.

## Próximo passo depois do retorno

No Windows, testar os binários na instalação de teste do gabinete. O teste real de Mercado Pago
somente deve ser feito depois de confirmar este retorno Linux e sempre com credenciais que o
proprietário possa revogar.
