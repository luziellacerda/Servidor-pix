# EmulationStation Suite 1.1.0 — contrato, operação e validação

A edição Suite reutiliza o TS e a chave CNG já ativados na mesma conta Windows.
Suite e ES têm sessões e desafios independentes. Não há ativação adicional,
chave nova, licença offline ou autorização por cache, MAC, IP ou cadastro comercial.
A ordem e a matriz de execução ficam no [handoff vigente](HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md).

**Correção local de 05/09/2026, ainda não implantada:** por decisão expressa do
usuário, reabrir o ES deve seguir a política da Suite: validar novamente e
substituir somente a sessão ES anterior da mesma licença/dispositivo. O release
de produção `34e31f2` ainda corresponde à política anterior de conflito; não
atribuir a ele a correção descrita abaixo. Build/testes locais de contrato
passaram; HTTP/PostgreSQL e homologação da reabertura real ainda estão pendentes.

## Rotas e assinatura

| Cliente | Desafio | Prova | Cabeçalho |
| --- | --- | --- | --- |
| Suite existente | `/v1/suite/challenges` | `/v1/suite/sessions` | ausente |
| ES 1.1.0 | mesmos caminhos Suite | mesmos caminhos Suite | exatamente `X-TurboRama-Client: EMULATIONSTATION` |
| ES dedicado 1.0.1 | `/v1/suite/emulationstation/challenges` | `/v1/suite/emulationstation/sessions` | ausente |

Cabeçalho vazio, repetido, CSV, desconhecido, com outra capitalização no valor,
em outro caminho ou método recebe 400 `CLIENT_SCOPE_INVALID`. O ES desabilitado
recebe 503 `EMULATIONSTATION_DISABLED`, sem encaminhamento alternativo à Suite.

O signer muda o Kind antes da assinatura, tanto no payload quanto no envelope:
`TURBORAMA_SUITE_ES_SESSION_OPEN_CHALLENGE`,
`TURBORAMA_SUITE_ES_SESSION_HEARTBEAT_CHALLENGE`,
`TURBORAMA_SUITE_ES_SESSION_OPEN`, `TURBORAMA_SUITE_ES_SESSION_HEARTBEAT`.
Os domínios originais Suite v1, produto, contextos e bytes da prova de máquina
permanecem iguais. O cliente valida o Kind e a assinatura antes de usar CNG.
Cabeçalho removido em qualquer etapa ou servidor antigo impede autorização.

A migration 023 acrescenta `client_contract` aos desafios ES. O padrão
`DEDICATED_V1` conserva os binários antigos; `SHARED_V1` impede consumir uma prova
nova pelo caminho dedicado. A escolha desse namespace não depende da política
de ocupação; os quatro Kind ES e os contratos v1 continuam iguais.

Na correção local, uma nova abertura compartilhada verifica prova CNG, vínculo,
licença, elegibilidade comercial e geração de revogação, consome seu desafio de
uso único e substitui atomicamente a sessão ES da mesma licença/dispositivo.
Responde com a assertion ES `ACTIVE` normal, sem nova ativação e sem exigir que
o operador encerre a sessão anterior no painel. Emitir apenas um desafio não
substitui a sessão; uma prova inválida também não a substitui. O heartbeat exige
o identificador da sessão vigente: o anterior não renova, inclusive com desafio
emitido antes da troca. Confirmações administrativas antigas continuam presas
ao alvo exato e não revogam a sessão nova. Suite e outros clientes são preservados.
O dedicado 1.0.1 mantém sua política existente de substituição e seu namespace.
Não há nova rota, ação `session.close`, migration ou alteração de TTL. Fechar o
programa não exige uma chamada de encerramento; na próxima abertura há nova prova.
O servidor não mata processos: a instância antiga perde renovação, e reage pelo
heartbeat/prazo já concedido. A trava local de instância não deve ser removida.

Heartbeat exige a mesma sessão ainda válida e renova por 180 segundos, com
intervalo indicado de cinco segundos. Abertura/heartbeat mantêm transações
serializáveis, travas por licença/dispositivo e até doze tentativas com jitter
para conflitos SSI/deadlock, dentro do timeout HTTP de dez segundos. Todas as
verificações e o consumo único são refeitos a cada tentativa; não há concessão
parcial. O atraso de cada repetição é limitado a 476 ms, com no máximo 4,522 s
de espera somada; execução e espera continuam limitadas pelo timeout HTTP.
O store Suite original também consome o desafio antes das gravações de sessão,
presença e outbox, seguindo a mesma ordem do store ES. Todas essas gravações
permanecem na mesma transação: uma falha desfaz também o consumo. Isso reduz o
trabalho e as dependências SSI acumuladas por tentativas abortadas.

## Painel existente e encerramento

As páginas `/admin`, `/admin/fragments/suite-clients`, `/admin/clientes/{licenseId}`
e `/admin/suite` exibem a sessão mais recente por aplicação, priorizando a vigente.
Os estados são Online, Sem contato recente, Expirada e Revogada. A migration 024
registra `last_contact_at` somente na abertura/renovação autenticada; timestamps
históricos desconhecidos continuam nulos. Online significa contato nos últimos
15 segundos, além de licença, dispositivo, vínculo e geração ainda válidos.

A listagem pagina 25 clientes e consulta até 50 linhas por lote, sem uma chamada
HTTP por cliente. A API aceita até 50 licenças distintas, retorna no máximo 100
linhas e usa timeout de cinco segundos. O polling ocorre a cada 15 segundos e
preserva filtros focados, diálogos e confirmações abertas.

O login administrativo existente concede `suite.sessions.read`,
`suite.sessions.revoke` e `suite.network.read`. Cookies emitidos antes da mudança
precisam de novo login para receber essas permissões. Não há conta humana nova:
vínculo CNG/licença é técnico e cadastro comercial não comprova login do titular.

O botão ES exige permissão específica, antiforgery, checkbox de confirmação e
senha administrativa recente. O alvo é protegido por Data Protection e vinculado
a ator, licença, dispositivo, sessão, request ID e validade de cinco minutos.
O BFF envia prova interna pelo socket existente. Token do socket sozinho não basta.

A transação trava a licença e compara o identificador exato da sessão (CAS).
Revoga somente essa linha ES, invalida os desafios pendentes daquela sessão e
registra recibo idempotente e auditoria. Repetição usa o recibo; confirmação antiga
não revoga sessão nova. A geração global, Suite, PIX e cliente B são preservados.
Negações administrativas também geram auditoria sem endereços de rede crus.

## Rede complementar

O contrato adicional está em `NetworkInventoryContract.cs`, idêntico ao cliente.
As rotas são `POST /v1/suite/network/challenges` e
`POST /v1/suite/network/inventory`, sem o cabeçalho ES. A ação é
`network.inventory.submit`, com escopo explícito `SUITE` ou `EMULATIONSTATION`.
Contexto: versão, produto, licença, dispositivo, sessão, aplicação, ação,
fingerprint existente, versão do cliente, instante de coleta e até oito interfaces.
Cada interface contém MAC canônico, tipo Ethernet/Wi-Fi, marcador local e virtual.

O desafio vincula o hash SHA-256 do contexto e expira em 60 segundos. A prova é
RSA-PSS-SHA256 usando `TurboRamaSuiteNetworkMachineProof/v1\0`; assertions usam
`TurboRamaSuiteNetworkAssertion/challenge/v1\0` e `.../result/v1\0`.
Kinds: `TURBORAMA_SUITE_NETWORK_CHALLENGE_V1` e
`TURBORAMA_SUITE_NETWORK_RESULT_V1`. O resultado `ACCEPTED` não concede acesso.
JSON é estrito e canônico, corpo máximo 8192 bytes, até oito desafios pendentes
por licença/dispositivo/aplicação; coleta aceita até cinco minutos de atraso.

O IP vem da conexão após a política existente de proxy confiável: somente
loopback, um salto e simetria dos cabeçalhos forwarded. `CF-Connecting-IP` e
X-Forwarded-For de origem não confiável não definem o IP. O proxy de destino deve
substituir cabeçalhos de origem, preservar o cabeçalho ES e encaminhar as duas
rotas adicionais de rede. Nenhuma mudança de proxy real foi declarada sem prova.

MAC/IP crus ficam cifrados com AES-GCM pela chave protegida do inventário existente.
O painel e seu papel PostgreSQL recebem somente máscaras. Retenção padrão de
30 dias, configurável entre 1 e 365; limpeza por minuto em lotes de 500. Mantém-se
apenas o relatório mais recente de cada licença/dispositivo/aplicação. A correção
do decrypt AES-GCM respeita o formato existente nonce/tag/ciphertext e é coberta
por teste de ida/volta e adulteração. O inventário original continua separado.

O coletor Windows usa interfaces físicas ativas, debounce mínimo de um minuto,
coleta inicial e por mudança/sessão, fora do heartbeat. A prova usa a chave CNG
existente. Falha do complemento não revoga, estende ou impede uma autorização.
IP é exclusivamente informativo, inclusive em análises combinadas; MAC isolado
não bloqueia, revoga, exige ativação nem altera fingerprint.

## Carga reproduzível

`tests/TurboRamaSuiteEmulationStation.Tests --load` cria 250 e 500 computadores
sintéticos com Suite+ES, totalizando 500 e 1000 sessões em um NAT. Mede abertura,
60 segundos de heartbeat a cada cinco segundos, rajada, retomada depois de
20 segundos sem rede e soak de 180 segundos com 1000 sessões.
A autoridade sintética RSA é 3072 bits, como a autoridade pública aprovada; as
máquinas sintéticas usam 2048 bits. Requisições passam por Kestrel e PostgreSQL real.

A primeira carga expôs varredura integral de desafios: parâmetros Npgsql `text`
comparados com `char(64)` não usavam o índice primário. A migration 025 cria índices
de expressão correspondentes, preservando bytes, política e transações existentes.
As consultas textuais usam `ix_suite_*_challenges_text_lookup`. O consumo Suite
materializa e trava primeiro a linha pela chave primária (`bpchar`, 64 caracteres),
depois aplica os filtros de consumo/expiração/geração à mesma linha. `EXPLAIN`
mostra `suite_challenges_pkey` seguido de `Tid Scan`; o localizador físico não sai
da instrução SQL. Isso impede a escolha do índice parcial de expiração para
varrer desafios de outros clientes durante uma transação serializável. Os testes
de corrida de ativação, desafio consumido/divergente e revogação permanecem.
A carga também motivou tentativas limitadas para contenção transitória. O cenário adicional de banco
recém-criado em dois núcleos mostrou que o pool de 32 conexões ainda gerava
contenção SSI entre sessões. O padrão foi reduzido para oito; um limite explícito
do operador continua respeitado. A relação entre concorrência, planos de consulta
e conflitos serializáveis consta na
[documentação do PostgreSQL 16](https://www.postgresql.org/docs/16/transaction-iso.html#XACT-SERIALIZABLE).
O workflow usa Bash com `pipefail`, inclusive ao gravar logs, para que falhas
interrompam a CI. Consultar a matriz para os resultados medidos e falhas corrigidas.

O relatório JSON registra latência HTTP p50/p95/p99, erros, vazão, memória/CPU
combinadas do gerador+API, conexões ativas, espera por lock e tamanho do rate limiter.
O teste local usa PostgreSQL limitado a 2 CPUs/1 GiB. O pool padrão da API é
8 conexões e o administrativo usa 8, reservando capacidade aos outros serviços;
um Maximum Pool Size explícito na conexão do operador permanece respeitado.
O gerador usa um pool independente de quatro conexões para fixtures/monitoramento. Não inclui latência de
internet, proxy público, TLS ou custo CNG real, nem comprova capacidade da produção.

## Pacote, configuração e rollback

O workflow servidor empacota API Suite, backend administrativo, servidor PIX/painel,
migrations 001–025, documentação, evidências e `SHA256SUMS.txt`, associados ao commit.
O cliente 1.1.1 gera EXE, ZIP portátil e ZIP de atualização exclusivos da edição
Suite. A CI conserva todos os testes existentes de contrato, DPAPI, IPC, extração,
ponte nativa, tema, pacote e preservação de áudio/memória/jogos. Teste automatizado
de preservação não substitui um PC Windows real de homologação.

No destino já autorizado: registrar versão e configuração efetivas, preparar backup
e rollback, aplicar apenas migrations ainda ausentes em ordem e habilitar
`Suite__EmulationStation__Enabled=true`, mantendo `Suite__Enabled=true`.
Para rede, exigir `Suite__Inventory__Enabled=true`, sua chave de inventário já
protegida e `Suite__NetworkInventory__Enabled=true`; configurar
`Suite__NetworkInventory__RetentionDays=30` conforme política do operador.
Não criar outra autoridade/chave CNG nem copiar material privado para o pacote.

**Não reutilizar o rollout histórico para esta correção.**
`ops/production/deploy-es-suite-20260905.py` continua fixado no artifact, SHA-256,
commit, diretório de release, CI e baseline anteriores de `34e31f2`; a guarda de
entrada agora bloqueia esse plano antes de acessar o host ou gravar relatórios.
Não há novo artifact/hash de produção inventado neste checkout. Após a CI,
o operador autorizado precisa conferir o novo `COMMIT.txt` e `SHA256SUMS.txt`,
registrar artifact/CI/hashes efetivos na matriz e revisar conjuntamente os pins,
binários/drop-ins atuais, backup e rollback do plano. Não basta alterar somente
`COMMIT` nem remover a guarda para executar `--apply` com o ZIP antigo. Esta
correção não acrescenta migration; preservar o schema 025 e a configuração atual.

O smoke em `ops/production/es-smoke/Program.cs` foi alinhado à nova reabertura:
open válido substitui apenas o ES anterior; heartbeat antigo e replay falham;
Suite, cliente B, anti-downgrade, revogação exata, rede e marcadores de zero
notificação de cliente continuam verificados. Build local net8.0 passou sem
executar `--production-smoke` nem `--tls-only`. Na implantação autorizada, compilar
esse smoke com `ServerPackageDir` apontando para a pasta `server` do **mesmo novo
artifact validado**, nunca para DLLs antigas; em seguida executar as verificações
no host pelo fluxo revisado. O smoke atualizado reprova corretamente um servidor
que ainda impõe conflito por ocupação, por isso não deve ser usado como prova de
falha do release histórico durante um rollback. A evidência
`docs/suite/evidence/es-deployment-20260905.json` permanece inalterada e identifica
somente os arquivos e o comportamento realmente implantados em `34e31f2`.

As migrations são aditivas e têm lock timeout de cinco segundos. A criação de
índices tem statement timeout de 30 segundos; em tabela grande, planejar a janela
antes de aplicar, sem remover timeouts para forçar produção. Testar compatibilidade
de binários anteriores com o schema atualizado. Rollback restaura binários e
configuração anteriores, desabilita as novas flags e conserva tabelas/índices e
auditoria; não exige apagar dados novos ou reverter o schema em funcionamento.

A implantação do servidor `34e31f2` foi concluída em 05/09/2026 às 18:23:58 UTC−3,
com autenticação nativa do operador, schema 025 e os três componentes saudáveis.
A verificação no endereço público confirmou as assinaturas, coexistência Suite/ES,
rede mascarada e revogação exata; o proxy existente foi preservado. O bloqueio
inicial de privilégios foi superado. Consultar a [matriz vigente](https://github.com/luziellacerda/Servidor-pix/blob/codex/emulationstation-suite-v1-20260905/docs/suite/HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md#04-matriz-unica-de-execucao)
para os hashes, backups, rollback exercitado e limites das medições de carga.
A release geral do cliente permanece pendente da homologação CNG/Windows real,
conforme o [handoff para o PC de produção](https://github.com/luziellacerda/Servidor-pix/blob/codex/emulationstation-suite-v1-20260905/docs/suite/HANDOFF-PC-PRODUCAO-ES-20260905.md).
