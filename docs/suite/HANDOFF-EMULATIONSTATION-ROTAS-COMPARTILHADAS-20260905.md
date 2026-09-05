# Handoff para a equipe do servidor: EmulationStation nas rotas Suite existentes

Data: 2026-09-05.

**Status: proposta de contrato e roteiro para trabalho futuro.** Este documento
nao implementa nem implanta as rotas compartilhadas. Nenhuma configuracao de
producao, chave, licenca ou sessao foi alterada para produzir este handoff. A
solicitacao desta etapa e publicar a orientacao no Git do servidor para a equipe
responsavel preparar, revisar e executar a integracao posteriormente.

## 1. Resultado solicitado pelo usuario

O EmulationStation da edicao sem servicos comerciais deve aceitar o identificador
`TS` que o cliente ja usa no TurboRama Suite e aproveitar a ativacao existente
desse computador. Informar esse identificador e aceitavel. Nao sera solicitado
outro codigo de ativacao, nem criada, substituida ou reativada uma identidade.

O cliente usa a mesma chave CNG existente da mesma conta Windows, e o servidor
confirma que a licenca continua vinculada e autorizada. A Suite e o EmulationStation
precisam poder funcionar juntos. O servidor PIX, pagamentos, locadora e os
demais fluxos existentes devem conservar seu comportamento.

O objetivo desta proposta e transportar as requisicoes ES pelos dois caminhos
Suite que ja existem no acesso publico, preservando a separacao das sessoes no
banco. **Compartilhar o caminho HTTP nao significa compartilhar a linha da
sessao nem transformar um cabecalho em autorizacao.**

Nao faz parte desta etapa implementar descoberta automatica de licenca por
`DeviceId`. O usuario esclareceu que pode informar somente o identificador `TS`.

O handoff incorpora tambem os requisitos complementares solicitados depois:

- conferir o vinculo autenticado de conta/licenca/dispositivo e permitir ao
  titular comprovado ou administrador encerrar uma sessao anterior, com confirmacao;
- mostrar no painel qual aplicacao possui sessao: TurboRama Suite ou EmulationStation;
- isolar centenas de clientes para que uma falha, disputa de sessao ou revogacao
  de um cliente nao prejudique os demais;
- acrescentar sinais de rede MAC/IP aos dados ja capturados, somente como apoio
  a analise de seguranca, preservando o inventario e a identidade existentes.

Esses complementos sao requisitos para desenho e implementacao futuros, nao
funcionalidades entregues por este documento. Devem ser desenvolvidos em etapas
separadas do despacho HTTP para manter revisao, compatibilidade e rollback claros.

## 2. Base conhecida e o que ja existe

Repositorio do servidor: `luziellacerda/Servidor-pix`.

Branch da extensao ES: `codex/emulationstation-suite-v1-20260905`.

Commit de referencia da implementacao existente:
`769f8b44c87b53ec6393276548a61da79b43aa22`.

Esse commit parte de `4ea972657355740485b3831970ef1fd21b186661`, da branch
`codex/turborama-suite-vendas-producao-20260828`. Ele adicionou a extensao ES com:

- `POST /v1/suite/emulationstation/challenges`;
- `POST /v1/suite/emulationstation/sessions`;
- `suite.suite_es_challenges` e `suite.suite_es_sessions`, migration 022;
- flag `Suite:EmulationStation:Enabled`, desabilitada por padrao;
- leitura das licencas, vinculos e dispositivos Suite ja existentes;
- verificacao criptografica pelo protocolo Suite e autoridade online existentes;
- testes sinteticos e testes reais de PostgreSQL em workflow proprio.

Os testes dessa base passaram nos seguintes runs do GitHub:

- [Extensao ES: migration e testes PostgreSQL](https://github.com/luziellacerda/Servidor-pix/actions/runs/33974034512).
- [Validacao existente do servidor](https://github.com/luziellacerda/Servidor-pix/actions/runs/33974034488).

Esses resultados correspondem a base acima. **Nao validam a proposta de rotas
compartilhadas deste documento**, que ainda nao foi implementada. Tambem nao
comprovam qual binario ou configuracao esta implantado no servidor publico.

Arquivos principais da base:

| Arquivo | Responsabilidade existente |
| --- | --- |
| `src/TurboRamaSuiteOnlineServer/Program.cs` | Registro de rotas e servicos, configuracao, timeout e erros |
| `src/TurboRamaSuiteOnlineServer/EmulationStationService.cs` | Restricao a abertura/heartbeat e endpoints ES dedicados |
| `src/TurboRamaSuiteOnlineServer/EmulationStationStore.cs` | Desafios/sessoes ES isolados, usando a identidade Suite existente |
| `src/TurboRamaSuiteOnlineServer/SuiteService.cs` | Validacao existente de licenca, contexto e prova de posse |
| `src/TurboRamaSuiteOnlineServer/Protocol.cs` | Serializacao canonica, produto, acoes e dominios de assinatura |
| `src/TurboRamaSuiteOnlineServer/Signing.cs` | Assinatura RSA-PSS das assertions |
| `src/TurboRamaSuiteOnlineServer/Store.cs` | Persistencia e sessoes originais da Suite |
| `migrations/suite/022_suite_emulationstation_sessions.up.sql` | Duas tabelas ES e suas permissoes |
| `tests/TurboRamaSuiteEmulationStation.Tests/Program.cs` | Testes existentes de isolacao, assinatura, revogacao e PostgreSQL |
| `.github/workflows/emulationstation-suite.yml` | Build/testes/artefato de revisao; nao faz deploy |

## 3. Evidencia publica observada e limite da conclusao

Foram feitas requisicoes GET sem licenca, sem prova CNG e com verificacao TLS
normal no acesso publico ja usado pela Suite:

| Caminho | Resposta observada |
| --- | --- |
| `/v1/suite/challenges` | HTTP 405, com `Allow: POST` |
| `/v1/suite/emulationstation/challenges` | HTTP 404, corpo vazio |

A conclusao sustentada e: o caminho dedicado ES nao estava disponivel naquele
acesso publico. Esses resultados **nao distinguem** um binario anterior sem a
extensao de uma regra de proxy/ingress que nao encaminha o novo caminho. Eles
tambem nao demonstram problema com a licenca, a chave CNG ou o computador.

Na base 769f8b4, `Program.cs` registra as rotas ES mesmo com a flag desabilitada.
O bloqueio por flag acontece dentro do handler POST. Assim, no processo correto:

- GET de rota registrada tende a devolver 405;
- POST `{}` com flag ES desabilitada devolve 503 e `EMULATIONSTATION_DISABLED`;
- POST `{}` com flag ES habilitada devolve 400 e `JSON_INVALID`, antes de acessar
  uma licenca ou concluir uma sessao;
- 404 nao e a resposta prevista apenas por desabilitar a flag dessa extensao.

O comportamento publico pode ser modificado pelo proxy. A distincao exige
comparacao com o listener local do servico, sem pressupor onde esta a falha.
Verificacao TLS normal nao substitui o teste dos pins e assertions do cliente.

## 4. Contrato proposto para as rotas compartilhadas

### 4.1 Caminhos e cabecalho de despacho

O futuro cliente ES enviara o mesmo corpo Suite v1 aos caminhos:

```text
POST /v1/suite/challenges
POST /v1/suite/sessions
X-TurboRama-Client: EMULATIONSTATION
```

O cabecalho e somente uma indicacao de qual implementacao atendera a requisicao.
A autorizacao continuara dependendo da prova RSA-PSS da chave cadastrada, da
licenca, do dispositivo, do contexto, do desafio e das verificacoes transacionais.
Conhecer o nome do cabecalho nao concede acesso.

Regras propostas para o dispatcher:

| Cabecalho/caminho | Comportamento obrigatorio |
| --- | --- |
| Cabecalho ausente nas duas rotas Suite | Executar o fluxo original Suite, sem mudanca de resposta ou tabela |
| Uma unica ocorrencia, valor exatamente `EMULATIONSTATION`, nas duas rotas Suite | Executar o fluxo ES com store isolado e signer com Kind ES |
| Cabecalho presente vazio, valor desconhecido, lista CSV ou repeticao | Rejeitar; nao interpretar como Suite nem escolher o primeiro valor |
| Cabecalho ES em ativacao, conteudo, inventario ou outro endpoint de operacao | Rejeitar; nao encaminhar a operacao para ES nem para o fluxo original |
| Cabecalho ES nas rotas dedicadas `/v1/suite/emulationstation/*` | Rejeitar como uso incorreto do contrato; compatibilidade dessas rotas continua sem esse cabecalho |
| Requisicao ES compartilhada com flag ES desabilitada | HTTP 503 / `EMULATIONSTATION_DISABLED`; nunca fallback para a sessao Suite |

Nome de campo HTTP segue a comparacao sem diferenciar maiusculas/minusculas do
protocolo HTTP. O valor deve ser comparado exatamente, sem aceitar aliases,
espacos adicionais preservados pelo servidor ou variantes de capitalizacao.
Duplicatas devem ser rejeitadas mesmo quando tiverem o mesmo valor. Uma unica
linha contendo valores separados por virgula tambem deve ser rejeitada.

Codigo de erro proposto para cabecalho/escopo invalido: HTTP 400,
`CLIENT_SCOPE_INVALID`, com mensagem publica generica. Esse novo codigo deve
ser documentado e coberto pelos testes quando a proposta for implementada.

### 4.2 Produto, corpo e prova de maquina preservados

Permanecem iguais:

- `schemaVersion = 1` e `productId = TURBORAMA_SUITE`;
- DTOs `ChallengeRequest`, `SessionContext`, `OperationProof` e `SessionProof`;
- acoes `session.open` e `session.heartbeat`;
- hash do contexto e todos os campos que ja compoem esse hash;
- prova de maquina e seu dominio `TurboRamaOnlineMachineProof/v1\0`;
- RSA-PSS-SHA256, SPKI, chave CNG, DeviceId e hardware fingerprint existentes;
- autoridade publica aprovada, chave de assinatura online e politica TLS;
- prazo de sessao, heartbeat, anti-replay, timeout e erros sanitizados;
- checagens de ativacao consumida, vinculo, estado da licenca/equipamento,
  geracao de revogacao e elegibilidade comercial ja presentes no store ES.

`\0` neste documento representa um byte NUL no dominio; nao os dois caracteres
barra invertida e zero. Nao reserializar ou alterar os bytes canonicos existentes.

### 4.3 Quatro Kind assinados para o ES compartilhado

O ES compartilhado deve receber Kind distintos dos da Suite original e dos
endpoints ES dedicados da versao 1.0.1:

| Resposta | Kind proposto |
| --- | --- |
| Desafio para `session.open` | `TURBORAMA_SUITE_ES_SESSION_OPEN_CHALLENGE` |
| Desafio para `session.heartbeat` | `TURBORAMA_SUITE_ES_SESSION_HEARTBEAT_CHALLENGE` |
| Resultado de `session.open` | `TURBORAMA_SUITE_ES_SESSION_OPEN` |
| Resultado de `session.heartbeat` | `TURBORAMA_SUITE_ES_SESSION_HEARTBEAT` |

O Kind deve constar tanto do envelope como do payload canonico assinado. Nao
basta acrescentar um campo HTTP ou modificar o envelope depois de assinar.

Os dominios das quatro assertions continuam sendo, respectivamente:

```text
TurboRamaSuiteOnlineAssertion/session-open-challenge/v1\0
TurboRamaSuiteOnlineAssertion/session-heartbeat-challenge/v1\0
TurboRamaSuiteOnlineAssertion/session-open/v1\0
TurboRamaSuiteOnlineAssertion/session-heartbeat/v1\0
```

A separacao proposta decorre do Kind dentro do payload assinado, de sua
validacao estrita pelo cliente e das tabelas isoladas de desafios/sessoes. Nao
e uma proposta de rotacao de dominio, produto, chave ou autoridade.

Implementacao minima sugerida: criar um wrapper de `IAssertionSigner`, usado
exclusivamente pelo servico ES compartilhado. Ele reconhece as quatro respostas
de abertura/heartbeat, troca o campo `Kind` do record antes da assinatura e
delega ao signer existente. Deve rejeitar tipos/acoes inesperados. Nunca registrar
esse wrapper como substituto global do signer da Suite ou dos endpoints legados.

O `RsaAssertionSigner` existente serializa o record canonico, assina os bytes e
monta o envelope a partir do mesmo record. Essa propriedade precisa ser
preservada; os testes devem verificar a assinatura e ambos os Kind.

### 4.4 Falha segura com servidor antigo ou cabecalho removido

O futuro cliente deve validar o Kind ES esperado **antes de assinar o desafio
ou enviar a prova de abertura/heartbeat**. Verificar apenas se a assinatura e
valida para a autoridade Suite nao e suficiente.

Sequencia com um servidor antigo que ignora o cabecalho:

1. ES pede um desafio pelo caminho compartilhado com o cabecalho.
2. O servidor antigo devolve desafio Suite com Kind antigo.
3. O cliente ES recusa esse Kind e nao assina nem envia a prova de sessao.
4. A sessao Suite em uso nao e substituida. Pode existir um desafio Suite nao
   consumido dessa tentativa; isso nao e abertura de sessao.

Nao adicionar fallback automatico para aceitar Kind Suite, reenviar sem
cabecalho ou concluir abertura usando o store original.

Se o cabecalho for removido somente na requisicao de conclusao, o dispatcher
original procurara o desafio na tabela Suite. O desafio ES nao existe ali e a
prova deve ser rejeitada. O cliente tambem deve recusar resultado com Kind
incompativel. Testar remocao tanto no desafio quanto na conclusao.

### 4.5 Compatibilidade com Suite e ES 1.0.1

A Suite original continua usando `/v1/suite/challenges` e `/v1/suite/sessions`
sem o novo cabecalho, com os mesmos Kind e bytes canonicos. O novo dispatcher
nao pode mudar esse caminho normal.

Os endpoints dedicados `/v1/suite/emulationstation/challenges` e
`/v1/suite/emulationstation/sessions` continuam atendendo clientes ES 1.0.1 sem
o novo cabecalho e com os Kind antigos. O signer original deve permanecer
nesse adapter. Nao remover essas rotas ao acrescentar as compartilhadas.

Ambas as entradas ES, dedicada e compartilhada, podem usar o mesmo store ES:
continua existindo uma sessao ES por licenca/equipamento. Abrir duas instancias
ES pode substituir a sessao ES anterior, conforme o contrato atual. Isso e
diferente de substituir a sessao da Suite, que deve continuar independente.

## 5. Limites de alteracao no servidor

Alteracoes esperadas na implementacao futura:

1. Parser estrito do cabecalho e decisao de despacho nas duas rotas existentes.
2. Adapter/servico ES compartilhado usando o `IEmulationStationStore` existente.
3. Wrapper de signer limitado aos quatro Kind ES novos.
4. Rejeicao do cabecalho em endpoints de operacao fora desse contrato.
5. Testes, documentacao do contrato e atualizacao do workflow de verificacao.

Manter `SuiteService.cs`, `Protocol.cs` e `Store.cs` originais sem mudancas de
logica sempre que possivel. Nao alterar as constantes Kind globais para que
todos os clientes passem a receber Kind ES. O wrapper deve ser local ao novo
caminho compartilhado.

Para o despacho HTTP compartilhado, a migration 022 existente ja oferece as tabelas necessarias.
Nao ha necessidade prevista de nova migration, nova tabela de licencas,
duplicacao de cadastro, troca de produto, nova ativacao ou novo registro CNG.
Se a implementacao revelar necessidade de schema adicional, registrar a razao
e revisar o contrato antes de alterar o banco.

Listagem por aplicacao, autorizacao de titular, encerramento direcionado e sinais
de rede podem exigir views, campos ou tabelas adicionais. Nesse caso, comparar
schema antes/depois e preparar migrations estritamente aditivas, com grants
minimos. Essa necessidade nao autoriza alterar DTOs canonicos v1, apagar dados,
recalcular fingerprints ou substituir cadastros/inventarios existentes.

Preservar expressamente:

- logica, rotas, servico, configuracao e estado comercial do PIX;
- protocolo Suite original e suas sessoes;
- autoridade, pins TLS, chaves e segredo de ativacao existentes;
- cadastro, licenca, vinculo, DeviceId e fingerprint do cliente;
- inventario, presenca, WhatsApp, catalogo, downloads, vendas e administracao;
- verificacoes financeiras e de revogacao que a extensao ES ja executa;
- isolamento transacional e anti-replay das tabelas ES.

O painel Suite existente e hospedado em componentes `SuiteAdminPanel.cs` e
`SuiteAdminBff.cs` dentro do projeto `TurboRamaPixOnlineServer`. Uma futura
extensao da area administrativa Suite nesses componentes deve ser revisada como
alteracao localizada de painel/BFF, sem mudar logica de pagamento, contratos ou
sessoes PIX. Nao confundir o nome do projeto hospedeiro com autorizacao para
refatorar o ecossistema PIX ou substituir sua configuracao de producao.

Nao acrescentar endpoint de ativacao no ES, permitir recriar chaves ausentes,
aceitar prova sem validar assinatura, flexibilizar TLS ou registrar dados
privados para facilitar diagnostico.

## 6. Verificacoes obrigatorias antes de liberar o contrato

Os testes devem usar somente fixtures sinteticas. Nunca preencher exemplos,
testes, workflow, commits ou logs com licencas reais, assinaturas reais de
clientes, identificadores de maquina ou credenciais de producao.

### 6.1 Testes de contrato e assinatura

- Sem cabecalho: a Suite conserva todos os vetores canonicos, Kind e respostas.
- Cabecalho exato: desafios/resultados usam os quatro Kind ES novos e assinatura
  valida da mesma autoridade, no envelope e no payload.
- Kind alterado depois da assinatura: rejeicao criptografica.
- Kind Suite antigo apresentado ao futuro cliente compartilhado: rejeicao antes
  de chamar a assinatura da maquina ou enviar prova.
- Cabecalho vazio, duplicado, CSV, variante de valor ou valor desconhecido:
  rejeicao sem queda para o caminho Suite.
- Cabecalho em ativacao, conteudo, inventario e endpoints ES legados: rejeicao.
- Flag ES desligada: 503 no ES compartilhado; Suite original permanece normal.
- Cliente ES 1.0.1 nos endpoints dedicados: comportamento anterior preservado.
- Remocao do cabecalho durante a ida do desafio ou da prova: nenhuma sessao Suite
  pode ser aberta/substituida pelo ES compartilhado.
- Produto, acao, contexto, licenca, dispositivo, fingerprint, nonce, desafio,
  assinatura ou prazo incorretos: as negativas anteriores continuam valendo.

As verificacoes de dispatcher devem passar pelo pipeline HTTP real de testes,
nao apenas chamar metodos do service. Testes de metodos isolados nao verificam
tratamento de `StringValues`, duplicatas, registro das rotas ou feature flag.

### 6.2 PostgreSQL real e nao interferencia

Executar no banco descartavel do workflow, com migrations 001–022:

- abrir Suite e ES compartilhado usando a mesma identidade sintetica;
- renovar as duas sessoes, inclusive com heartbeat concorrente;
- provar que abrir/reabrir ES altera somente a sessao ES;
- provar que a sessao ES nao autoriza catalogo/download Suite;
- enviar desafio/prova ES ao caminho Suite sem cabecalho e o inverso;
- repetir a mesma prova, usar desafio vencido e usar sessao substituida;
- suspender/revogar, incrementar geracao, retomar e rejeitar provas antigas;
- negar equipamento revogado, ativacao nao consumida e heartbeat sem abertura;
- manter negacao de sessao ES expirada e da entrega comercial inelegivel;
- confirmar que nenhum teste cria/reativa licenca ou altera identidade para
  compensar uma falha de integracao.

Conservar os testes originais Suite e a validacao do servidor PIX. Os resultados
aprovados de 769f8b4 nao dispensam repetir as verificacoes apos implementar o
dispatcher e o signer compartilhados.

### 6.3 Conta, titularidade e encerramento de sessao anterior

O servidor pode verificar uma identidade autenticada, sua autorizacao, a licenca
e o dispositivo vinculado. Isso nao permite afirmar que e a mesma pessoa fisica
por comparar IP, nome do Windows, nome do computador, MAC ou identificador TS.
Uma assinatura CNG comprova posse da chave cadastrada naquele contexto; nao
constitui, por si, um login autenticado de uma conta humana.

Evidencia do modelo atual:

- `Store.cs` representa licenca, equipamento, vinculo e sessao; nao possui um
  principal de conta cliente autenticada nesses records.
- `suite.suite_license_enrollments` tem uma licenca por chave de equipamento
  cadastrada (`device_id` unico); esse vinculo nao deve ser trocado automaticamente.
- `SuiteAdminPanel.cs` carrega um ledger comercial de clientes e define
  `SuiteCustomerLicense`, com CustomerId, nome, email e LicenseId, entre outros
  campos. Esse dado ajuda a localizar o cadastro no painel, mas nao comprova
  que o solicitante e o titular da conta. O arquivo de dados nao foi lido neste trabalho.

Antes de liberar autosservico ao titular, identificar um vinculo confiavel,
mantido no servidor, entre principal autenticado, conta comercial e licenca.
Se esse fluxo de conta autenticada nao existir, registrar a lacuna e manter a
operacao restrita ao administrador autorizado ou ao fluxo de recuperacao Suite
ja aprovado. Nao inventar autenticacao de titular com base somente no ledger,
no identificador TS enviado, no Windows ou em sinais de rede.

A opcao de interface sera **"Encerrar sessao anterior"**, com confirmacao
explicita e dados mascarados da sessao-alvo. Nao executar o encerramento apenas
porque outra sessao foi encontrada. Conta divergente sem autoridade comprovada
deve receber negacao ou orientacao de atendimento administrativo; nao uma opcao
capaz de derrubar a sessao de outro cliente.

O encerramento padrao deste pedido tem escopo **EMULATIONSTATION**. Ele revoga
uma sessao ES selecionada no servidor. Nao executa `systemctl stop`, nao encerra
processo da API, PIX, loja Suite ou emulador, nao altera a licenca e nao migra
chaves/dispositivos. A troca de PC continua sujeita a politica de transferencia
Suite existente e a sua autorizacao especifica.

Contrato adicional a ser congelado antes de implementar encerramento:

1. Autorizar o ator no servidor: titular autenticado com vinculo confiavel e
   prova CNG apropriada, ou administrador com permissao especifica e step-up.
   Um administrador autorizado nao precisa possuir a chave privada do cliente;
   segue a fronteira administrativa existente.
2. Vincular a operacao a produto, conta/vinculo autorizado, licenca, dispositivo,
   `appScope`, `targetSessionId`, `expectedSessionId` ou versao da instancia,
   identificador de requisicao, desafio e prazo.
3. Para uma operacao iniciada pelo cliente, exigir prova CNG sobre esse contexto
   canonico, nonce imprevisivel, TTL curto e uso unico. Definir dominio e tipo
   assinados proprios para essa operacao; nao reutilizar uma prova de abertura
   como permissao de encerramento nem acrescentar campos ao contrato v1 existente.
4. Revalidar autorizacao do ator e estado atual dentro da transacao. Aplicar CAS
   ao alvo esperado: uma confirmacao antiga nao pode revogar a sessao que foi
   criada depois da tela de confirmacao. Heartbeats normais nao devem causar
   confusao entre a versao da instancia e um timestamp que muda a cada renovacao.
5. Revogar somente a linha ES selecionada e invalidar os desafios que possam
   renovar aquela sessao, de forma atomica e auditavel. Nao incrementar uma
   geracao global de licenca para encerrar uma unica sessao ES: isso atingiria
   a Suite e outros contextos fora do alvo solicitado.
6. Tratar repeticao/requisicao concorrente de forma idempotente ou como conflito
   seguro, nunca redirecionando a mesma ordem para uma sessao mais nova.
7. Aplicar rate limits e auditoria com ator autenticado, escopo, alvo interno,
   resultado e correlacao; mascarar dados de cliente na interface/logs. Nao
   registrar provas, chaves, corpos completos, MAC/IP ou identificador TS em claro.

A revogacao no servidor impede o heartbeat posterior do alvo. A interface do
cliente reage conforme o mecanismo de autorizacao e o prazo ja concedido.
Nao prometer encerramento instantaneo de um processo Windows, especialmente
quando o equipamento estiver sem rede. Nao estender TTL nem conceder modo
offline para esconder esse intervalo de reconciliacao.

Encerrar uma sessao nao e suspender a licenca para sempre. Se o objetivo futuro
for bloquear novas aberturas ou recuperar conta/dispositivo, usar a politica
administrativa apropriada, separadamente e com confirmacao correspondente.

A base atual permite que `session.open` substitua a sessao ES anterior. A nova
experiencia de conflito/confirmacao deve ter contrato proprio para o cliente
compartilhado e ser testada com ES 1.0.1; nao alterar silenciosamente o fluxo
normal da Suite nem eliminar a compatibilidade declarada na secao 4.5.

Testes obrigatorios: titular autorizado, conta divergente, identificador TS
sozinho, administrador sem claim, CSRF/step-up ausentes, prova trocada, replay,
alvo de outro cliente/aplicacao, confirmacao antiga, abertura concorrente,
heartbeat apos revogacao e manutencao das restricoes financeiras. Encerrar A
nunca deve afetar a licenca, a sessao nova ou as sessoes de B.

### 6.4 Painel: qual programa tem uma sessao autorizada

Apresentar uma listagem autorizada de sessoes, com rotulos definidos pelo
servidor: **TurboRama Suite** e **EmulationStation**. `appScope` deve decorrer
do contrato atendido e da tabela da sessao; no ES compartilhado, tambem dos
Kind assinados. Nao aceitar nome de programa arbitrario enviado pelo cliente.

Campos minimos: aplicacao, conta/vinculo quando comprovado, dispositivo
mascarado, identificador de sessao mascarado, ultimo contato/heartbeat conhecido,
validade e estado. Se nao houver principal de conta cliente comprovado, exibir
essa limitacao em vez de inventar "mesma pessoa".

Estados de apresentacao devem ser derivados no servidor:

- **Online:** sessao autorizada, valida e com contato recente segundo o intervalo
  de heartbeat existente e uma tolerancia explicitamente definida.
- **Sem contato recente:** prazo ainda vigente, mas sem renovacao recente.
- **Expirada:** prazo encerrado.
- **Revogada:** sessao/geracao invalida ou revogacao aplicavel confirmada.

"Online" significa presenca recente inferida do protocolo. Nao prova que um
processo esta aberto naquele instante, nem identifica uma pessoa. Nao coletar
uma lista de processos do Windows, aplicativos arbitrarios ou telemetria fora
do escopo das aplicacoes licenciadas.

A migration 020 define `suite_device_presence` com chave
`(license_id, device_id)`, sem `appScope`. Somar um rotulo nessa presenca unica
nao distingue Suite de ES. A listagem deve ler `suite_sessions` e
`suite_es_sessions`, por exemplo por uma view aditiva com escopo fixo em cada
ramo, preservando o significado da presenca e das notificacoes Suite atuais.

Verificar a semantica dos timestamps antes de nomear uma coluna "ultimo
heartbeat": `last_server_time` e monotonicamente ajustado por
`GREATEST(anterior+1, agora)`, e nem todo caminho original atualiza `updated_at`
como instante de contato. Nao apresentar uma data de criacao como ultima
renovacao. Qualquer ajuste de metadados necessario deve ser aditivo, documentado
e sem mudar a contagem de validade ou o protocolo de autenticacao original.

Reutilizar a fronteira descrita em `docs/suite/CONTENT-ADMIN-BFF-CONTRACT.md`:
host administrativo protegido, sessao/autorizacao administrativa, claims,
antiforgery e step-up para mutacoes. O BFF encaminha ator e claims validados
ao backend administrativo; o token do Unix socket sozinho nao basta. Definir
permissoes especificas de ler sessoes e encerrar sessoes, com grants minimos.
Nao tornar os dados disponiveis em uma API publica consultavel so pelo TS.

O botao de encerramento se refere a uma unica linha e obedece a secao 6.3.
O padrao e sessao ES; uma eventual operacao administrativa sobre sessao Suite
precisa de escopo, permissao e confirmacao separados, sem reaproveitar
implicitamente a autorizacao dada para ES.

Testes: Suite e ES simultaneos aparecem como duas sessoes distintas; nomes sao
fixos e valores exibidos sao escapados contra XSS; filtros, pagina/contagem e
acoes nao vazam outro cliente; nenhum identificador completo ou sinal de rede
aparece inadvertidamente em HTML, URL, logs ou mensagens; estado online segue
TTL/heartbeat e nao um teste de processo remoto.

### 6.5 Centenas de clientes: capacidade e isolamento entre contas

Tratar a ausencia de interferencia entre clientes como criterio de aceite,
nao somente como preferencia de interface. Toda leitura/acao deve resolver no
servidor o escopo autorizado de conta/licenca, dispositivo, aplicacao e sessao.
Nao confiar em trocar um ID no corpo ou URL para escolher outro titular.

Usar chaves, caches, rate limits e estado separados por escopo. Constraints e
locks devem proteger o alvo correto, sem um bloqueio global que pare todos os
clientes. A transacao pode bloquear a licenca envolvida para preservar regras
existentes; nao pode serializar clientes independentes numa unica fila global.

Ponto concreto para revisar: `SuiteRateLimiter` atual usa chave composta por
origem, bucket, rota, licenca e dispositivo, sem aplicacao; possui limites de
cardinalidade por origem e do bucket. Nas rotas compartilhadas, Suite e ES
passariam a usar a mesma rota HTTP. A implementacao deve manter namespaces
adequados e testar NAT com centenas de clientes antes de escolher os limites.
Nao usar um IP compartilhado como prova de mesma pessoa nem negar todo um NAT
legitimo porque um unico cliente apresentou erro.

Combinar limitacao pre-autenticacao por origem confiavel com limites do
principal/licenca/dispositivo/aplicacao apos autenticacao. Aplicar tambem
limites de cardinalidade, backlog e tempo, sem permitir que clientes novos
esgotem todos os slots de clientes ativos. Uma mudanca nos limites atuais
precisa ser explicita e verificada, nao um efeito colateral do dispatcher.
Esses controles operacionais de volume nao podem decidir vinculo, elegibilidade
ou estado de licenca a partir do valor ou da troca de IP. A telemetria de IP e
qualquer score que a consuma nao podem disparar negacao de acesso. Dimensionar
os limites para o trafego legitimo agregado de NAT; nao impor bloqueio coletivo
indiscriminado nem penalizar um cliente autorizado por compartilhar a origem.

Estimativa de trafego, **nao capacidade medida**: cada sessao com heartbeat de
cinco segundos faz aproximadamente duas requisicoes HTTP por renovacao
(desafio e conclusao). Admitindo todas as sessoes ativas simultaneamente:

| Cenario assumido | Sessoes | Estimativa sustentada |
| --- | --- | --- |
| 250 PCs com Suite e ES | 500 | 200 requisicoes HTTP/s |
| 500 PCs com Suite e ES | 1000 | 400 requisicoes HTTP/s |

Esses valores excluem login inicial, tentativas extras, painel, inventario e
outros servicos. Nao garantem que o host, assinatura RSA, banco ou proxy atuais
suportem a carga. Medir antes de liberar para centenas de clientes.

Plano de carga: 500 e 1000 sessoes, inclusive clientes atras de NAT comum,
aberturas em rajada, reconexao apos indisponibilidade, teste sustentado e soak.
Registrar latencias p50/p95/p99, taxa de erros por escopo, CPU, memoria, tempo
de assinatura/verificacao, pool/conexoes PostgreSQL, locks, filas, timeouts e
estabilidade da renovacao antes do vencimento. Definir limites de aceite no
ambiente real; nao prometer capacidade sem essas medicoes.

Manter pools, timeouts, filas e backpressure limitados. Reconexao deve ter
backoff/jitter sem prolongar a autorizacao local. Nao mudar silenciosamente o
heartbeat de cinco segundos da Suite, desativar verificacoes ou aceitar modo
offline para fazer um teste de carga passar.

Painel: paginacao e tamanho maximo obrigatorios, indices compativeis com os
filtros, atualizacao em frequencia limitada, sem varredura global a cada polling
e sem N+1 por cliente. Listar uma linha nao cria heartbeat nem faz uma chamada
de licenciamento adicional. Totais e caches tambem respeitam permissao/conta.

Testes de nao interferencia obrigatorios: falha, sessao encerrada, erro de
assinatura, bloqueio financeiro, revogacao, reconexao ou carga excessiva do
cliente A nao alteram licenca, sessao ou resposta autorizada de B. Testar
encerramento concorrente de sessao antiga enquanto uma nova e aberta, e provar
que nenhum alvo novo ou de outro cliente e revogado por engano.

### 6.6 MAC/IP como complemento ao inventario e a seguranca existentes

O pedido e **aditivo**: conservar os dados ja capturados, campos de inventario,
fingerprints, baseline de placa-mae, autoridade, protocolos, vinculos e regras
atuais. MAC/IP complementam a analise; nao substituem nem recalculam a identidade
CNG/hardware, nao apagam o historico e nao exigem nova ativacao.

IP deve ser observado pelo servidor a partir da conexao e da cadeia de proxies
confiaveis ja estabelecida. Reutilizar `SuiteTrustedProxyPolicy` e a configuracao
de ingress aprovada. Nunca acreditar diretamente em `X-Forwarded-For` ou
`CF-Connecting-IP` enviados por um cliente fora dessa cadeia. Normalizar IPv4,
IPv6 e formas equivalentes antes de comparar sinais.

**Regra obrigatoria do usuario: IP e somente informativo/diagnostico.** O valor,
a mudanca ou a divergencia de IP nunca podem bloquear a licenca, revogar ou
encerrar uma sessao, negar a reconexao do usuario ja autorizado nem exigir nova
ativacao. IP nao entra no fingerprint, na identidade canonica nem em um score
que dispare essas medidas indiretamente. IP compartilhado nao vincula clientes
como a mesma pessoa e nao pode motivar encerramento ou bloqueio coletivo.
As verificacoes de licenca e prova CNG continuam obrigatorias e independentes
do IP; mudar de rede nao dispensa nem acrescenta uma exigencia de ativacao.

Preservar os controles operacionais existentes contra abuso de trafego, mas
nao transformar rate limit em verificacao de identidade por IP nem usar troca
de IP como gatilho punitivo. A revisao de capacidade e limites deve acomodar
centenas de clientes legitimos atras de NAT, sem negar esse uso apenas por
compartilharem a origem. Qualquer mudanca operacional necessita revisao e
testes proprios; este requisito nao autoriza desativar protecoes existentes.

MAC da placa de rede nao atravessa uma requisicao HTTP pela internet. Para
obte-lo, uma futura versao do cliente precisa informar explicitamente um sinal
coletado localmente. Isso ainda nao existe como resultado deste handoff.
Limitar a coleta a interfaces relevantes ativas/fisicas e um limite pequeno,
por exemplo oito interfaces, validado no cliente e no servidor. Definir forma
canonica do MAC, tipo de interface e marcadores de endereco administrado
localmente/randomizado/virtual, com deduplicacao. Nao coletar inventario de
processos, arquivos, nomes arbitrarios ou informacoes sem relacao com esse fim.

Transmitir o complemento por HTTPS e prova CNG da identidade existente,
vinculada a licenca, dispositivo, aplicacao, sessao, contexto, nonce e TTL,
com anti-replay. O servidor observa o IP; um IP declarado pelo cliente nao
substitui essa observacao. A assinatura autentica a origem da declaracao de MAC,
mas nao torna o MAC verdadeiro ou imutavel.

Reutilizar a fronteira de inventario da migration 020 e seus controles de
validacao/protecao. Projetar DTO/versao/acao complementar aditivos quando
necessario; nao acrescentar campos aos DTOs v1 estritos nem alterar seus bytes
canonicos. Um cliente antigo continua transmitindo o inventario original.
Comparar schema antes/depois e exigir migration sem destruicao quando houver
novos campos ou tabelas. Nenhuma atualizacao automatica do fingerprint original
ou baseline de hardware e autorizada por uma divergencia de MAC/IP.

Correlacao permitida no escopo autorizado: MAC/IP com DeviceId CNG,
hardware fingerprint existente, inventario de placa-mae ja coletado, aplicacao,
sessao e conta autenticada quando houver vinculo confiavel. Exibir indicios
explicaveis para revisao autorizada, preservando incerteza e mascarando valores.

MAC pode ser alterado, virtualizado ou randomizado. IP muda por DHCP, VPN,
rede movel, provedor e NAT; diversos clientes legitimos podem compartilhar IP,
e um computador pode possuir varias interfaces. Nenhum desses sinais identifica
uma pessoa. Divergencia isolada de MAC nao deve bloquear, revogar, transferir
ou agrupar automaticamente licencas como se fossem a mesma conta. Para IP a
regra e mais forte: seu valor ou mudanca nao participa de nenhuma decisao de
bloqueio, encerramento, elegibilidade ou nova ativacao, mesmo combinado com
outros sinais em um score. Um sinal do cliente A nunca deve produzir bloqueio
geral de B ou de todos atras do NAT.

Minimizar quantidade de interfaces, frequencia e retencao. Definir retencao
configuravel e acesso restrito, com mascaras no painel e criptografia em repouso
coerente com o inventario existente. Se forem usados hashes de correlacao,
usar HMAC com chave apenas no servidor e dominio/namespace apropriados; nao
usar um hash simples de MAC/IP como anonimato nem permitir correlacao
indiscriminada entre contas. Dados reais, MAC/IP e chaves nao entram em Git,
build, argumentos/query strings, logs ou mensagens publicas.

A coleta deve ser transparente e limitada as aplicacoes licenciadas. Coletar
no login ou em mudanca relevante, com debounce e limites; nao reenviar a lista
a cada heartbeat de cinco segundos. Reaproveitar sinais ja existentes quando
adequado, sem reduzir a coleta original autorizada. Falha de telemetria
complementar nao deve cancelar silenciosamente o licenciamento existente.

Testes obrigatorios: cabecalho IP falsificado, prova/payload adulterados,
replay, licenca ou sessao errada, excesso de interfaces, MAC invalido/repetido,
MAC randomizado/local/virtual, IPv4/IPv6, mudanca legitima de rede, NAT com
centenas de clientes e indisponibilidade do coletor. Verificar isolamento de
dados/permissoes, mascaras, retencao e carga adicional limitada. Mudanca de IP
nao pode disparar bloqueio direto/indireto ou nova ativacao, e mudanca de MAC
isolada tambem nao autoriza essas medidas. Testar
explicitamente DHCP, VPN, transicao 4G/5G/Wi-Fi e troca IPv4/IPv6: abertura e
heartbeat de cliente autorizado continuam normais, sem revogacao, encerramento,
negacao de reconexao ou bloqueio por score associado ao IP. Varias licencas
validas no mesmo NAT devem continuar independentes e operantes.

## 7. Roteiro operacional futuro — nao executado por este handoff

A documentacao historica aponta `turborama-suite-api.service` e o listener Suite
`127.0.0.1:5190`. Ela cita releases sob `/opt/turborama-suite-r5-releases/`. Isso
e referencia para verificacao, nao prova do destino atual. Nao ha aqui credencial
SSH, conexao de banco ou autorizacao para acessar o host de producao.

### 7.1 Confirmar o processo e a diferenca entre origem e acesso publico

Na futura etapa autorizada no host, consultar metadados da unidade sem imprimir
variaveis privadas ou arquivos de credenciais:

```bash
systemctl show turborama-suite-api.service \
  -p FragmentPath -p DropInPaths -p ExecStart -p WorkingDirectory -p User
```

Confirmado o listener correto, os seguintes exemplos consultam apenas a rota e
o parser; nao usam licenca nem concluem sessao:

```bash
curl -sS -i http://127.0.0.1:5190/v1/suite/emulationstation/challenges

curl -sS -i -X POST -H 'Content-Type: application/json' --data '{}' \
  http://127.0.0.1:5190/v1/suite/emulationstation/challenges
```

Comparar com o caminho publico aprovado. Se a origem reconhece a rota mas o
publico devolve 404, investigar encaminhamento. Se a origem devolve 404, confirmar
binario/processo/listener efetivos. Nao corrigir isso reativando a maquina.

Para as rotas compartilhadas futuras, testar tambem a propagacao exata do
cabecalho. Um servidor antigo pode devolver o mesmo erro de JSON para `{}` com
ou sem cabecalho; esse teste sozinho nao prova suporte ao novo contrato. A prova
de suporte exige testar os Kind novos com identidade sintetica no ambiente
apropriado ou com cliente autorizado na homologacao.

### 7.2 Preparar e habilitar a implementacao aprovada

Somente apos o contrato ser implementado, revisado e aprovado:

1. Identificar o commit/binario e a configuracao realmente carregados no host.
2. Preservar o release anterior e a configuracao para rollback.
3. Confirmar migrations 001–021. Se 022 ainda estiver ausente, aplicar somente
   `migrations/suite/022_suite_emulationstation_sessions.up.sql`, uma vez, na
   conexao Suite previamente confirmada. Exemplo de comando, sem credenciais:

   ```bash
   psql --set=ON_ERROR_STOP=1 \
     --file=migrations/suite/022_suite_emulationstation_sessions.up.sql
   ```

4. Publicar o artefato revisado em novo diretorio de release e atualizar o
   apontamento apenas da unidade Suite, preservando os arquivos protegidos e as
   autoridades existentes. Nao substituir configuracoes privadas por exemplos.
5. Habilitar `Suite__EmulationStation__Enabled=true` na configuracao do servico
   Suite, preservando `Suite__Enabled` e as demais variaveis. O recurso deve
   continuar false por padrao no repositorio e nos artefatos.
6. Confirmar que o proxy mantem as rotas Suite existentes e transmite o novo
   cabecalho. Nao encaminhar requisicoes ES ao processo PIX. Nao remover rotas
   dedicadas usadas por ES 1.0.1. Validar qualquer ajuste de Nginx antes de reload.
7. Recarregar a configuracao e reiniciar somente a API Suite quando necessario:

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl restart turborama-suite-api.service
   curl --fail --silent --show-error http://127.0.0.1:5190/health
   curl --fail --silent --show-error http://127.0.0.1:5190/ready
   ```

8. Homologar abertura e heartbeat das duas aplicacoes, isolacao das sessoes,
   negacoes previstas, suporte ao cliente antigo e funcionamento normal dos
   demais servicos. Health/ready sozinhos nao comprovam o contrato criptografico.

As etapas de listagem, titularidade/encerramento e sinais de rede precisam de
plano de publicacao separado conforme seus componentes efetivamente alterados
(API, backend administrativo, painel/BFF e eventual coletor cliente). O roteiro
de reiniciar somente a API nao instala por si uma funcionalidade nova no BFF.
Confirmar previamente o escopo de cada implantacao e preservar o ecossistema
existente; este documento nao autoriza paradas, conexoes ou mudancas de producao.

Nao usar `ops/deploy-round12.sh` como instalador Suite: aquele script e do PIX e
opera outra aplicacao. O workflow ES atual gera artefato de revisao e executa
testes; nao deve receber deploy automatico como efeito colateral deste trabalho.

### 7.3 Rollback

Desabilitar `Suite__EmulationStation__Enabled` e, se necessario, voltar o
apontamento da API Suite ao release anterior, preservando sua configuracao.
Revalidar Suite e servicos existentes. A indisponibilidade ES deve ser apresentada
como tal ao cliente, sem fallback para consumir a sessao original da Suite.

A migration 022 pode permanecer instalada; nao e necessario apagar tabelas ou
registros para voltar ao binario anterior. Nao executar down migrations
destrutivas como rotina de rollback. Nao restaurar chaves ou reativar licencas
como forma de contornar uma falha de roteamento.

## 8. Checkpoints para a equipe do servidor e para o frontend

Antes de publicar uma nova versao frontend que use as rotas compartilhadas,
a equipe do servidor deve entregar:

1. **Contrato confirmado:** caminhos, cabecalho exato, quatro Kind, compatibilidade
   e comportamento de falha revisados com a equipe frontend.
2. **Implementacao revisavel:** branch/commit e diff limitado ao dispatcher,
   adapter/signer ES, testes e documentacao; demonstracao de que o caminho Suite
   sem cabecalho, o PIX e as regras de licenca permaneceram preservados.
3. **CI aprovado:** testes de protocolo, HTTP, compatibilidade e PostgreSQL real
   para o commit que sera publicado, incluindo servidor antigo/cabecalho removido.
4. **Artefato identificado:** commit e SHA-256 do pacote/binario, configuracao
   padrao desabilitada e procedimento de rollback correspondente.
5. **Destino confirmado:** processo/binario, listener, migration 022, flag e
   encaminhamento efetivos verificados no ambiente autorizado, sem divulgar
   credenciais nem dados privados.
6. **Homologacao conjunta:** cliente compartilhado reconhece os Kind ES e abre
   a sessao isolada com a ativacao existente; Suite e ES continuam autorizados
   simultaneamente; ES 1.0.1 permanece compativel.
7. **Titularidade e painel:** lacuna de conta autenticada resolvida por vinculo
   confiavel ou operacao restrita a administrador; sessoes separadas por
   aplicacao, permissoes/CSRF/step-up verificados, encerramento com confirmacao
   e alvo exato, sem afetar sessoes novas, Suite, PIX ou outros clientes.
8. **Escala medida:** testes de 500/1000 sessoes, NAT, rajada/soak, pool/locks e
   latencias registrados; isolamento entre clientes e limites aprovados para
   o host medido. Nao substituir evidencia por promessa de capacidade.
9. **Sinais complementares:** contrato MAC/IP separado e aditivo, origem IP
   confiavel, coleta limitada, privacidade/retencao/testes definidos e nenhuma
   alteracao de inventario, fingerprint, autoridade ou vinculo existentes.
   IP e estritamente informativo: nenhuma mudanca/valor/divergencia pode provocar
   bloqueio direto ou indireto, encerramento, negacao de reconexao ou reativacao.
   Se a coleta depender de frontend novo, nao anuncia-la antes dessa entrega.
10. **Liberacao coordenada:** comunicar o commit/artefato do servidor validado e
   a disponibilidade do contrato. So entao liberar a versao frontend correspondente.

O frontend deve manter a exigencia da chave CNG existente na mesma conta Windows,
validar rigorosamente os novos Kind antes de produzir a prova, usar somente o
identificador ja ativado e explicar indisponibilidade do servidor sem pedir nova
ativacao. Este handoff nao autoriza enfraquecer essas verificacoes para que um
cliente novo funcione contra um servidor antigo.

## 9. Estado de entrega desta etapa

Foi criado somente este documento. O contrato compartilhado continua proposto,
nao implementado nem implantado. Titularidade/encerramento, listagem por programa,
capacidade multi-cliente e complemento MAC/IP tambem sao requisitos futuros
documentados, nao funcionalidades executadas nesta etapa. Nenhum arquivo de runtime, teste, workflow,
migration, autoridade ou configuracao foi alterado nesta etapa. Nao houve
conexao SSH, leitura de chave privada, uso de licenca real ou operacao em
producao. A equipe que executar a etapa seguinte deve registrar os checkpoints
acima e distinguir testes sinteticos de homologacao real do ambiente autorizado.
