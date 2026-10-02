# Handoff de retorno — auditoria de publicação do app Station

Data da medição: **02/10/2026, 18h49 (America/Maceió)**. Base documental: `docs/handoff-humano-station-20261002` em `c3061bd`. Este retorno responde se o servidor e o APK podem ser liberados a compradores. Consulte o [handoff humano](HANDOFF-HUMANO-COMPLETO-STATION-20261002.md) para o contrato de integração.

## Decisão

**Servidor acessível para integração e teste controlado. APK ainda não aprovado para publicação a compradores.** O [retorno do app](RETORNO-APP-PRODUCAO-STATION-20261002.md) continua sem medição no aparelho: o empacote e a instalação no Windows/Samsung não foram concluídos nesta máquina. Não registrar esse retorno como teste aprovado até medir a compilação real.

O único endereço do APK é `https://app.lzgames.com.br/v1/station/`. O prefixo `/v1/station/` é obrigatório; `/v1/` sozinho também atende outras aplicações neste host.

## Fatos observados nesta auditoria

| Verificação | Resultado |
| --- | --- |
| Git remoto | `docs/handoff-humano-station-20261002` em `c3061bd`; fonte indicada `feat/station-library-grant-20261002` em `b1159c9` |
| API Station | `turborama-station-api.service` ativo, PID 281271, `ExecStart` com DLL em `/opt/turborama-station-20261001/`; nenhum drop-in |
| DLL efetiva | SHA-256 `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`, igual ao handoff |
| Escuta | 5190, 5191, 5192 e 5194 somente em `127.0.0.1` |
| Origem Station | `GET /ready/station` local: 200 |
| Borda HTTPS | `GET /v1/station/catalog`, `/me` e capa sem Bearer: 401; grant inexistente: 404, sem `Location`; `cf-cache-status: DYNAMIC` e `cache-control: no-store` |
| TLS | Hash SPKI da chave pública apresentada em `app.lzgames.com.br` confere com o pin do handoff |
| Serviços compartilhados | PIX, Suite API, administração, gateway, nginx, cloudflared e PostgreSQL ativos; health local de PIX, Suite API e gateway: 200 |
| Monitor Suite | `turborama-suite-content-monitor.service` continua `failed`; o último erro registrado é `DATABASE_PROBE_HEALTH`. A falha já aparece no inventário anterior à integração Station; não há evidência de que Station a causou. |

O inventário somente de leitura de `scripts/inventario-somente-leitura.sh` foi executado. Nenhum serviço foi reiniciado, nenhuma configuração foi alterada e nenhuma licença ou sessão foi criada nesta auditoria.

## O que a medição ainda não prova

1. **Lista real e quantidade.** Os logs do processo atual mostram três respostas 200 para `/catalog`, três para `/me` e nove aberturas de sessão, mas o conteúdo assinado não foi lido nesta auditoria. O handoff humano informa **996 itens**. O número **1.816** em `RETORNO-APP-PRODUCAO-STATION-20261002.md` é de uma medição anterior e não deve ser usado como quantidade atual. Confirmar `items.length` com uma sessão autorizada no aparelho.
2. **Capas.** Desde o início do processo atual há 48 respostas 404 para `/covers/{coverId}` e nenhuma 200 registrada. Um 404 pode significar ID ausente do índice **ou** arquivo de capa ausente/inválido; os logs não distinguem as causas. Fazer GET com um `coverId` extraído do catálogo autenticado e registrar apenas status, tipo e tamanho, sem ID ou nome.
3. **Jogo.** Não há 200 registrado para `/downloads/authorize` nem `/artifacts/{grantId}` nesta medição. O 404 para grant inexistente prova recusa, não prova entrega. Testar uma autorização válida, primeiro GET 200 com `Content-Length`, segundo GET 404 e ausência de `Location`/URL pública.
4. **Health.** A implementação de `/ready/station` consulta a migration `028_station_android` no banco. Ela não verifica existência dos arquivos do índice, das capas, da chave de grants nem o caminho completo de download. Um 200 nessa rota não substitui o teste autenticado.
5. **APK.** Não há artefato novo, diff de dex, instalação preservando dados ou teste de abrir jogo no aparelho neste retorno. O arquivo de retorno do APK permanece pendente.

## Sequência para fechar a publicação

1. Na máquina Windows, empacotar o APK conforme [handoff do app](HANDOFF-APP-PRODUCAO-STATION-20261002.md), preservando assinatura, dados, jogos e saves. Registrar versão, hash e diff do pacote sem copiar segredos.
2. No Samsung já vinculado, instalar com `adb install -r --no-incremental`. Sem reemitir código ou reativar, comprovar sessão renovável, `/me` e `/catalog` 200, assinatura válida, quantidade do catálogo e cache privado.
3. Usar um `coverId` recebido desse catálogo para comprovar capa 200. Abrir um jogo ainda ausente localmente para comprovar `authorize` 200, artefato 200, tamanho íntegro e segundo uso 404. Confirmar que login não dispara download e que a gamelist não contém URL.
4. Preencher e publicar [RETORNO-APP-PRODUCAO-STATION-20261002.md](RETORNO-APP-PRODUCAO-STATION-20261002.md) com resultados do aparelho, inclusive falhas. Não incluir senha, token, chave, device ID, grant ID, nome de jogo ou caminho de arquivo.
5. Diagnosticar o monitor Suite `failed` antes da liberação ampla, sem desativar a validação. Conferir a saúde dos serviços compartilhados e manter evidência de backup/restauração e plano de retorno da release do APK.

**Critério de decisão:** liberar o APK somente após o retorno do aparelho comprovar lista, capa e jogo completos e a falha operacional do monitor estar entendida/resolvida. Até lá, o servidor pode receber testes controlados do cliente.
