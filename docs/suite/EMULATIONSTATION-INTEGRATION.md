# EmulationStation Suite 1.1.0 — contrato, operação e validação

A edição Suite reutiliza o TS e a chave CNG já ativados na mesma conta Windows.
Suite e ES têm sessões e desafios independentes. Não há ativação adicional,
chave nova, licença offline ou autorização por cache, MAC, IP ou cadastro comercial.
A ordem e a matriz de execução ficam no [handoff vigente](HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md).

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
nova através da política de substituição do caminho dedicado.

Uma nova abertura compartilhada encontra conflito se já existir sessão ES ativa.
Depois de verificar prova, vínculo, elegibilidade comercial e consumir o desafio
em transação, responde HTTP 200 com assertion ES assinada, status `CONFLICT` e
`authorizedUntilUnixSeconds == serverTimeUnixSeconds`: nenhuma janela de acesso.
O cliente apresenta `ES_SESSION_CONFLICT` somente depois da validação criptográfica.
A sessão existente continua. O legado dedicado 1.0.1 conserva sua substituição
silenciosa; essa limitação não foi removida dos clientes já distribuídos.

Heartbeat exige a mesma sessão ainda válida e renova por 180 segundos, com
intervalo indicado de cinco segundos. Abertura/heartbeat mantêm transações
serializáveis, travas por licença/dispositivo e até doze tentativas com jitter
para conflitos SSI/deadlock, dentro do timeout HTTP de dez segundos. Todas as
verificações e o consumo único são refeitos a cada tentativa; não há concessão
parcial. O atraso de cada repetição é limitado a 476 ms, com no máximo 4,522 s
de espera somada; execução e espera continuam limitadas pelo timeout HTTP.
A mudança no store Suite original limita-se a esse orçamento de tentativas.

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
EXPLAIN deve mostrar `ix_suite_*_challenges_text_lookup`. A carga também motivou
as tentativas limitadas para contenção transitória. O cenário adicional de banco
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
O cliente 1.1.0 gera EXE, ZIP portátil e ZIP de atualização exclusivos da edição
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

As migrations são aditivas e têm lock timeout de cinco segundos. A criação de
índices tem statement timeout de 30 segundos; em tabela grande, planejar a janela
antes de aplicar, sem remover timeouts para forçar produção. Testar compatibilidade
de binários anteriores com o schema atualizado. Rollback restaura binários e
configuração anteriores, desabilita as novas flags e conserva tabelas/índices e
auditoria; não exige apagar dados novos ou reverter o schema em funcionamento.

Somente publicar release de cliente para uso após verificar binários, migrations,
flags e proxy do servidor de destino e coexistência em Windows. Enquanto esse
acesso estiver indisponível, os artefatos permanecem candidatos de CI. O executor
local identificado nesta entrega não tem sudo sem senha para os serviços, e o
executor permitido `turborama-isolated-exec` aponta para namespace ausente.
