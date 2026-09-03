# Handoff — notificação WhatsApp ao conectar no TurboRama Suite

## Objetivo

Quando uma licença válida abrir uma sessão autorizada no TurboRama Suite, enviar uma notificação WhatsApp ao proprietário da conta. A notificação deve ocorrer somente no evento `session.open`; heartbeats não podem gerar mensagens repetidas.

## Repositórios e arquivos

- Servidor Suite: `/home/lz-servidor/turborama-download/Servidor-pix`.
- Serviço online: `src/TurboRamaSuiteOnlineServer`.
- Fluxo de sessão: `src/TurboRamaSuiteOnlineServer/SuiteService.cs`, método `SessionAsync`.
- Adaptador já criado: `src/TurboRamaSuiteOnlineServer/TurboRamaWhatsAppNotifier.cs`.
- API interna já criada no TurboRama: `POST /internal/turborama/whatsapp/connection`.
- TurboBox separado: `/home/lz-servidor/staging/turbobox-suite-vendas-producao-20260828`.
- Rota TurboBox: `POST /turborama/whatsapp`.
- Fila existente no TurboBox: tabela SQLite `notification_jobs`.
- Worker existente: `process-notifications.php`.

## Implementação necessária

1. No `SuiteService.SessionAsync`, depois de `CompleteSessionAsync` retornar sucesso e antes de devolver a asserção, reconhecer somente `request.Proof.Action == "session.open"`.
2. Resolver o telefone do proprietário a partir da licença (`licenseId`). Nunca aceitar telefone arbitrário vindo do cliente do programa.
3. Montar mensagem com licença, horário, dispositivo mascarado e resultado autorizado. Não incluir serial/UUID completos ou segredos.
4. Chamar o endpoint interno `/turborama/whatsapp` com `X-TurboRama-Internal-Token`.
5. A chamada deve ser best-effort: timeout curto, captura de erro e logging sem impedir o login autorizado.
6. Usar idempotência por `licenseId + sessionId + event=session.open`; não enviar em `session.heartbeat`.

## API TurboBox

O TurboBox permanece independente. A rota `/turborama/whatsapp` valida o token interno, normaliza o telefone, evita duplicidade e insere na `notification_jobs`. O worker MenuIA faz o envio externo.

Endpoint oficial de envio MenuIA:

`POST https://chatbot.menuia.com/api/create-message`

JSON: `appkey`, `authkey`, `to`, `message`. As chaves devem permanecer somente em variáveis de ambiente; nunca devem ser commitadas.

## Variáveis de ambiente

TurboRama:

- `TURBORAMA_INTERNAL_TOKEN`
- `TURBORAMA_MENUIA_ENDPOINT` (fallback oficial: `https://chatbot.menuia.com/api/create-message`)
- `TURBORAMA_MENUIA_APPKEY`
- `TURBORAMA_MENUIA_AUTHKEY`

TurboBox deve manter suas próprias variáveis `TURBOBOX_*`; não compartilhar banco, fila ou chaves de licenciamento.

## Segurança e invariantes

- Não modificar RSA/TLS, fingerprints, chaves, licenças, migrations ou regras de autorização.
- Não bloquear nem alterar a resposta de `session.open` quando o WhatsApp estiver indisponível.
- Não enviar mensagens para números fornecidos pelo cliente sem validação administrativa.
- Não registrar appkey, authkey, token interno, serial ou UUID completo nos logs.
- Não usar `/pagamento/webhook` para mensagens; ele permanece exclusivo de pagamentos.

## Build, deploy e rollback

1. Executar `dotnet build src/TurboRamaSuiteOnlineServer/TurboRamaSuiteOnlineServer.csproj -c Release`.
2. Criar release imutável com hash do DLL.
3. Configurar variáveis protegidas no serviço systemd.
4. Publicar o release e reiniciar somente `turborama-suite-api.service`.
5. Manter release anterior disponível para rollback imediato.

## Critérios de aceite

- Conexão autorizada (`session.open`) retorna HTTP 200 mesmo se MenuIA estiver fora do ar.
- Uma mensagem é colocada na fila para `82993474007` durante o teste controlado.
- Repetição do mesmo `sessionId` não cria mensagem duplicada.
- `session.heartbeat` não cria mensagem.
- Licença inválida continua negada exatamente como antes.
- `/health` e `/ready` retornam HTTP 200 após o deploy.
- Nenhum segredo aparece no Git ou nos logs.

## Estado atual

O adaptador e os endpoints estão versionados, mas a chamada no `SuiteService.SessionAsync` e a resolução segura `licenseId → telefone` ainda precisam ser implementadas pelo servidor de produção.
