# Ordem unica de execucao completa: EmulationStation integrado a Suite

Data: 2026-09-05.

## Prioridade atual — executar a correção de reabertura no servidor

**Encaminhamento solicitado pelo usuário em 05/09/2026, 19:48 UTC−3.** O usuário
testou o EXE 1.1.2 correto e voltou a receber “Já existe uma sessão EmulationStation
neste computador”. A ordem é concluir a correção no servidor, não entregar outro
EXE, refazer ativação, limpar sessões de clientes ou responder apenas que a CI passou.
Usar este mesmo handoff para execução e retorno; não criar outra cadeia de documentos.

### Evidência do PC e limite desta entrega

- EXE em uso: `D:\TURBOPCINSTALL\build\emulationstation\emulationstation.exe`;
  905674752 bytes; SHA-256
  `9feebb133fbf81ce9fe55e3bce7b4408ab7e1ca39958287fef62b106af7bbbab`.
- Esse hash é o cliente 1.1.2, commit
  `187b72686888580e75c5edcef52f5c031601cf5b`, CI `33994557511` aprovada. A cópia
  baixada foi verificada e o mesmo hash foi confirmado na instalação utilizada.
- Na observação de aproximadamente 19:43 UTC−3 havia uma instância do ES e seu
  helper filho. A tela novamente mostrou conflito de sessão. Isso não é evidência
  de segundo ES local, de necessidade de abrir a Suite ou de nova ativação.
- O texto corresponde ao tratamento cliente de `ES_SESSION_CONFLICT`; conferir
  no servidor a resposta e o processo que realmente a produziu. O último registro
  confirmado de produção é `34e31f2`, que ainda tem a regra antiga de ocupação.
- Da tarefa Windows, SSH respondeu mas não houve confirmação de chave de host nem
  autenticação. A tentativa foi encerrada a pedido do usuário, que escolheu o
  encaminhamento para a tarefa do servidor. **Nenhuma implantação ocorreu daqui.**

### Pacote aprovado a implantar — não recompilar por suposição

| Identificação | Valor verificado |
| --- | --- |
| Branch servidor | `codex/emulationstation-suite-v1-20260905` |
| Commit de runtime | `efaf1d3cd3dfd2a807e9d5a0e7295328ff081c4a` |
| CI da integração | [33994510188 — success](https://github.com/luziellacerda/Servidor-pix/actions/runs/33994510188) |
| CI geral | [33994510175 — success](https://github.com/luziellacerda/Servidor-pix/actions/runs/33994510175) |
| Artifact | [9977821782](https://github.com/luziellacerda/Servidor-pix/actions/runs/33994510188/artifacts/9977821782) |
| Nome | `servidor-suite-emulationstation-review-efaf1d3cd3dfd2a807e9d5a0e7295328ff081c4a` |
| Tamanho do arquivo do artifact | 2864186 bytes |
| SHA-256 do artifact | `ab017ad8313fc0c50e702c4d6aa7ae7f8348276376a8ea850f04a19ac0c1cf86` |

Os commits posteriores de documentação não são outro binário: conferir
`COMMIT.txt` e cada entrada de `SHA256SUMS.txt` do pacote. A diferença de runtime
`34e31f2 → efaf1d3` está somente em `EmulationStationStore.cs` e
`SharedEmulationStation.cs`, dentro da API Suite. Admin, PIX, contratos comuns e
migrations não mudaram; os projetos admin/PIX não carregam a DLL da API.

### Execução mínima no host autorizado

1. Atualizar a referência da branch sem sobrescrever trabalho local e confirmar
   acesso administrativo pelo mecanismo já usado no Linux. Identificar o estado
   **real**, não apenas o Git: unidade, PID, caminho do DLL carregado, hash,
   `WorkingDirectory`, argumentos, drop-ins, saúde e schema 025. O baseline
   histórico da API é
   `/opt/turborama-suite-r5-releases/es-suite-34e31f2-20260905/server/TurboRamaSuiteOnlineServer.dll`,
   SHA-256 `8a8a90c9a623c155dabeeb3ba88ef0c983964bd603aa3b0240dd1df5fee4c568`.
   Se estiver diferente, revisar o delta e preservar quaisquer correções novas;
   não impor esse baseline antigo ou rebaixar produção.
2. Baixar e verificar o artifact aprovado no servidor. Preparar a pasta publicada
   `server/` **completa** em um novo diretório de release, preservando a anterior;
   não copiar só o DLL nem substituir dependências em uso. Conferir os hashes
   antes da troca. Não aplicar migrations novas: esta correção não tem migration.
3. Preparar backup protegido da configuração e rollback **somente da API** antes
   do reinício. Preservar o drop-in atual `zzzz-es-suite-20260905.conf`; usar uma
   alteração adicional e reversível de `ExecStart` da
   `turborama-suite-api.service`, mantendo os argumentos necessários. Manter
   `WorkingDirectory`, contas/permissões, credenciais systemd, autoridades,
   pepper, AES, conexão PostgreSQL, inventários/rede e retenção existentes.
   Preservar `Suite__Enabled` e `Suite__EmulationStation__Enabled` efetivos:
   o pacote sai com ES desabilitado por padrão, sem autorizar desligá-lo no host.
4. Trocar/reiniciar **apenas `turborama-suite-api.service`**. Não reiniciar nem
   trocar `turborama-pix.service` ou `turborama-suite-admin.service` por esta
   correção. A API também atende Suite e conteúdo: preparar a troca para reduzir
   interrupção, sem prometer zero impacto. Não depender do TTL de 180 s como
   garantia de que todo cliente tolerará o reinício.
5. Verificar o novo PID/caminho/hash efetivo, `/health`, `/ready` e
   `/ready/content`; conferir o encaminhamento público já existente. Se ainda
   houver `CONFLICT`, identificar todos os destinos/processos realmente servidos
   pelo proxy, incluindo instâncias antigas, antes de alterar novamente código.
   Não mudar proxy, CNG, TLS ou assinaturas para contornar a negativa.
6. Compilar/executar no host o smoke atualizado em `ops/production/es-smoke/`,
   passando `ServerPackageDir` como caminho absoluto para a pasta `server/` do
   **mesmo artifact `9977821782` validado**, não o diretório histórico padrão do
   projeto. Usar fixtures sintéticas, limpeza limitada às fixtures e zero notificações reais.
   Exigir nova abertura validada `ACTIVE`, negação de heartbeat da sessão antiga,
   replay negado, isolamento Suite/ES/cliente B e revogação administrativa por alvo
   exato. Não usar a premissa antiga de que toda segunda abertura deve dar conflito.
7. Depois da verificação no host, solicitar o teste **com o mesmo EXE 1.1.2 já
   instalado**: Suite fechada, entrar, sair normalmente, confirmar encerramento
   de ES/helper, reabrir imediatamente e validar de novo. Repetir ciclos e testar
   coexistência com Suite e outra identidade sintética. Não exigir apagar cache,
   aguardar três minutos, encerrar pelo painel ou ativar a licença novamente.

**Rollback correto:** desfazer somente a alteração adicional da API e restaurar
seu alvo anterior confirmado; verificar saúde novamente. **Não executar** o
rollback histórico `.../es-suite-34e31f2-20260905T212345Z/rollback.sh`: ele remove
três drop-ins e volta os três serviços a versões anteriores a `34e31f2`.
O script `ops/production/deploy-es-suite-20260905.py` também está fixado em um
rollout antigo e bloqueado por guarda. Não basta retirar essa guarda ou trocar um
hash: preparar/revisar o plano API-only de acordo com o baseline real.

### Critério de conclusão e retorno no mesmo arquivo

Registrar horário/fuso, commit/pacote/hashes, novo PID/caminho efetivo, resultado
de saúde e smoke, rollback preparado e resultado do teste Windows de reabertura.
Confirmar que PIX/admin não foram trocados e que flags/chaves/sessões de outros
clientes foram preservadas. Não registrar TS completo, MAC/IP bruto, senhas,
tokens, strings de conexão ou material privado. Manter a evidência JSON histórica
intacta; não rebatizar o resultado antigo como implantação nova.

Se faltar privilégio, artefato, baseline seguro ou teste no PC, identificar o
bloqueio exato e a única ação necessária; **não declarar corrigido em produção**.
O trabalho de capacidade/400 req/s e os warnings antigos do cliente continuam
fora desta correção. Não modificar mais o cliente para compensar o backend antigo.

**ORDEM VIGENTE: IMPLEMENTAR, TESTAR E CONCLUIR TODO O ESCOPO ABAIXO.** O usuario
corrigiu expressamente a orientacao anterior: nao quer outra rodada apenas de
leitura, proposta, recebimento ou handoff. O executor deve realizar o trabalho
de engenharia e a entrega verificavel, nao somente responder que compreendeu.

Esta revisao substitui a limitacao documental do commit `f1bb86a` e a sequencia
de encaminhamento do retorno `db4a992`. Os dois registros permanecem no historico
como evidencia do que foi feito, nao como instrucao para adiar a implementacao.
O arquivo conserva o mesmo caminho para manter uma unica referencia vigente.
Publicar esta ordem nao significa que o codigo ja foi implementado ou implantado:
esses resultados so podem ser declarados com as evidencias exigidas abaixo.

**Atualização de produto em 05/09/2026 — correção no Git, não implantada:** o usuário
determinou que a reabertura do ES funcione como a TurboRama Suite: validar de novo
com o vínculo/chave existentes e substituir a sessão ES anterior do mesmo PC.
Esta decisão substitui somente a exigência anterior de conflito/confirmar pelo
painel a cada reabertura. A administração por alvo exato continua disponível.
Não foi criada API `session.close`, nova ativação ou migration. O registro de
implantação `34e31f2` abaixo permanece histórico: ainda descreve a trava anterior.
Servidor: `efaf1d3cd3dfd2a807e9d5a0e7295328ff081c4a`,
[CI da integração 33994510188](https://github.com/luziellacerda/Servidor-pix/actions/runs/33994510188).
Build/testes locais e **CI integral aprovada**: protocolo, HTTP/PostgreSQL,
banco original Suite, painel Chromium e carga 500/1000. Artifact `9977821782`,
SHA-256 `ab017ad8313fc0c50e702c4d6aa7ae7f8348276376a8ea850f04a19ac0c1cf86`.
Implantação da correção e homologação Windows continuam pendentes.
Na carga desta CI houve 114000 respostas HTTP 200 e zero falhas de troca nas
fases medidas. Isso é regressão em ambiente isolado, não SLA: com 1000 sessões,
o soak mediu 240,82 req/s, p99 de 6589,568 ms e duração de 298,973 s. Não comprova
400 req/s no servidor real; a otimização/homologação de capacidade continua adiada
conforme a decisão do usuário. Nenhum limiar ou teste foi relaxado nesta correção.
O cliente correspondente é o candidato **1.1.2**, commit
`187b72686888580e75c5edcef52f5c031601cf5b`, em
[compilação 33994557511](https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33994557511).
Ele encerra o helper de forma limitada, cancela o login sem erro e mostra o
ícone existente. Não depende de manter a Suite aberta, mas preserva sua chave
CNG/ativação na mesma conta Windows. **CI do cliente aprovada**, artifact
`9977794150`, SHA-256 do EXE
`9feebb133fbf81ce9fe55e3bce7b4408ab7e1ca39958287fef62b106af7bbbab`.
O passo de release foi omitido pelo gate manual, não por falha. Teste real
continua pendente; detalhes e avisos no handoff do PC, sem novo documento paralelo.
O smoke operacional já foi alinhado e compilado localmente, sem execução no host;
o script de rollout antigo foi bloqueado por guarda antes de acessar produção,
pois seus pins continuam em `34e31f2`. Preparar o próximo plano somente após a CI
confirmar artifact/hashes e o operador conferir o baseline atual, conforme o
[runbook existente](EMULATIONSTATION-INTEGRATION.md). A evidência JSON de
implantação anterior permanece intacta, sem atribuir a ela testes novos.

## 0. Ordem de trabalho integral, sem novos ciclos de handoff

### 0.1 Como executar esta ordem

1. Atualizar a branch ES indicada na secao 2, preservar alteracoes de outros
   colaboradores e inspecionar o estado real dos dois repositorios e do ambiente
   disponivel. Nao partir da `main` antiga nem sobrescrever a branch PIX.
2. Implementar todas as frentes da secao 0.2 nesta mesma tarefa. Separar codigo,
   commits e testes por responsabilidade para facilitar revisao, mas nao separar
   o pedido em novas rodadas de autorizacao ou documentos de encaminhamento.
3. Resolver os detalhes internos de DTOs adicionais, schema aditivo, permissoes,
   limites e testes durante a implementacao. Registrar as decisoes concretas no
   codigo e na documentacao tecnica existente; nao devolver somente uma lista
   do que outra pessoa ainda precisaria definir.
4. Executar verificacoes locais seguras e CI no GitHub, corrigir falhas e repetir
   os testes afetados. Manter Suite e PIX originais funcionando. Nao marcar a
   tarefa concluida apenas porque o dispatcher ou uma compilacao passaram.
5. Produzir os artefatos finais separados, realizar a implantacao/homologacao no
   destino quando o executor ja possuir acesso e autorizacao para esse ambiente,
   e verificar os resultados reais. Nao inventar destino, credencial ou sucesso.
6. Manter somente uma matriz de execucao neste arquivo e entregar um unico resumo
   final com commits, testes, artefatos e estado de implantacao. Nao criar arquivos
   sucessivos de HANDOFF/RETORNO para substituir trabalho de implementacao.

As revisoes e os checkpoints tecnicos deste documento sao etapas internas desta
ordem, nao solicitacoes para o usuario aprovar cada frente novamente. Uma falha
de build/teste exige diagnostico e correcao, nao um novo pedido de handoff.

Se houver bloqueio real de acesso, credencial, permissao, destino ou dependencia
externa indispensavel, concluir primeiro o trabalho independente e informar de
uma vez os bloqueios restantes, com a verificacao feita e a acao minima necessaria.
Nao insistir em producao sem autoridade, enfraquecer autenticacao ou simular
homologacao para atender a expressao "fazer tudo". Pendencia real deve permanecer
explicitamente pendente; nao e sucesso nem motivo para abandonar outras frentes.

### 0.2 Frentes obrigatorias da mesma entrega

| Frente | Trabalho a executar, nao apenas documentar | Evidencia de conclusao |
| --- | --- | --- |
| Licenca e rotas compartilhadas | Implementar dispatcher estrito, adapter/store ES, signer com quatro Kind e validacao cliente antes de assinar; usar a ativacao Suite existente | Testes HTTP e criptograficos, PostgreSQL e cliente/servidor compativeis; Suite sem cabecalho e ES 1.0.1 preservados |
| Titularidade e isolamento | Resolver identidade tecnica CNG/licenca/dispositivo e vinculo comercial confiavel; aplicar autorizacao por alvo e separar estado/caches/limites por cliente e aplicacao | Testes cruzados A/B e entre apps, sem acesso ou revogacao fora do escopo; nenhuma nova ativacao |
| Painel de sessoes | Implementar listagem protegida e paginada de Suite e ES, estados, ultimo contato correto e dados mascarados | Duas aplicacoes simultaneas visiveis como linhas distintas, permissoes e XSS testados |
| Encerrar sessao anterior | Implementar confirmacao e revogacao atomica da sessao ES selecionada com CAS, auditoria e controle de concorrencia | Alvo exato revogado; sessao nova, Suite, PIX e cliente B preservados; replay/CSRF/step-up testados |
| MAC/IP complementares | Implementar contrato aditivo autenticado, persistencia protegida e coletor cliente limitado; IP observado pelo servidor e MAC declarado pelo cliente | Dados complementares disponiveis no painel autorizado, sem alterar inventario/fingerprint existentes; IP nao causa bloqueio nem por score |
| Capacidade | Implementar limites e consultas adequados e executar carga com 500 e 1000 sessoes, NAT, rajada/reconexao e soak | Relatorio medido de latencias, erros, recursos e isolamento; limites efetivamente suportados declarados |
| Cliente e distribuicao | Integrar contrato, tela de acesso existente, telemetria e tratamento de revogacao; compilar no GitHub e publicar artefatos da edicao Suite protegida | EXE/ZIP, versao, commit e SHA-256 identificados; regressao de audio/memoria/jogos e seguranca aprovada |
| Servidor e homologacao | Empacotar, aplicar migrations aditivas necessarias e configurar/publicar no destino autorizado com rollback; verificar proxy e funcionamento conjunto | Commit/binario/migrations/flag/proxy efetivos registrados; testes conjuntos e verificacao de nao interferencia |

### 0.3 Decisoes de produto ja tomadas para nao travar a execucao

- **Acesso:** um unico identificador TS ja ativado; usar a chave CNG existente
  da mesma conta Windows. Nao pedir novo codigo, recriar chave, transferir PC ou
  alterar o ecossistema de ativacao. Nao fixar uma licenca real no codigo.
- **Titularidade:** nao criar um novo sistema de contas humanas so para esta
  entrega. Reutilizar autenticacao de titular se ela realmente existir e for
  verificavel. Na ausencia dela, entregar o gerenciamento pelo administrador
  autenticado no painel existente; mostrar o vinculo tecnico e o cadastro
  comercial com rotulos honestos. Essa alternativa ja esta decidida e nao
  justifica deixar painel/encerramento apenas no papel.
- **Encerramento:** o botao administrativo atua por padrao em uma unica sessao
  ES com confirmacao e permissao especifica. Nao precisa da chave privada do
  cliente. Autosservico do titular so usa um fluxo autenticado ja comprovado;
  nao liberar a operacao apenas com TS, IP, MAC ou dados do ledger.
- **Reabertura ES, decisão revisada:** seguir a Suite original. Uma nova abertura
  com prova CNG válida e todas as revalidações transacionais substitui somente a
  sessão ES da mesma licença/dispositivo, mesmo se a anterior ainda estiver ativa.
  O SessionId anterior não renova, nem com desafio de heartbeat já emitido; a
  instância nova recebe sua própria sessão em memória, nunca a sessão do cache.
  Desafio sem prova, chave/dispositivo incorretos, licença inelegível e replay não
  substituem a sessão. Preservar SHARED_V1, quatro Kind, TTL, locks e geração;
  Suite, PIX, outros clientes e confirmações administrativas por alvo exato não
  mudam. Não pedir ação no painel para fechar/reabrir normalmente, nem criar
  `session.close`. DPAPI não é autorização. A trava local de instância permanece.
  Preservar o dedicado ES 1.0.1 conforme 4.5. Esta correção ainda não foi implantada.
- **IP:** exclusivamente informativo, inclusive quando cruzado com outros dados.
  Nunca entra em bloqueio direto/indireto, revogacao, encerramento, impedimento de
  reconexao, fingerprint ou exigencia de nova ativacao. MAC tambem nao e prova
  de pessoa e divergencia isolada nao autoriza essas medidas.
- **Telemetria:** limitar a ate oito interfaces relevantes; coleta inicial e por
  mudanca com debounce, nao a cada heartbeat. Definir e testar limites de payload,
  retencao configuravel e protecao dos novos dados. Nao reduzir ou apagar o
  inventario original para acomodar o complemento. Falha do coletor nao cancela
  uma autorizacao valida.
- **Custo e seguranca:** nao contratar servico, certificado ou recurso pago; usar
  os ambientes e limites ja disponiveis. Nao desativar Defender, criar exclusoes
  ou repetir comandos do InstallerHost que geraram alertas. Nao publicar chaves,
  identificadores reais, tokens ou material privado de autoridade nos artefatos.
  Preservar as autoridades publicas aprovadas que o cliente precisa verificar.

### 0.4 Matriz unica de execucao

Atualização em **05/09/2026 às 18:23:58 (America/Maceio, UTC−3)**: API Suite,
backend administrativo e PIX/painel implantados e verificados no destino real.
As migrations 022–025 estão aplicadas e a integração ES/rede está habilitada.
A homologação com a chave CNG existente no PC Windows continua pendente; o
operador já dispõe do 1.1.1 e recebeu a orientação de repetir a abertura.

| Frente | Estado verificado | Evidência |
| --- | --- | --- |
| Rotas/licenciamento compartilhado | Implementado, testado e implantado | Servidor `34e31f26b6a864a7aa5d701b94fe29ad166e86ac`; cliente `ada45558611bdd98ca0a5ed9053fdd97ff85a067`; cabeçalho estrito, quatro Kind, conflito assinado sem autorização, anti-replay e anti-downgrade |
| Correção de reabertura como a Suite (decisão posterior) | Commit `efaf1d3`, **CI integral aprovada; não implantado** | CI `33994510188`, artifact `9977821782`; substituição validada, negação de heartbeat antigo, replay/CAS/A-B, banco e painel aprovados. Teste real de fechar/reabrir pendente; preservar o registro histórico de produção |
| Titularidade, painel e encerramento | Testes funcionais/Chromium aprovados; backend real verificado | Suite/ES aparecem separadamente; permissões/CSRF/step-up/CAS; revogação exata e idempotente, desafio pendente invalidado e nova prova de heartbeat negada; Suite e cliente B preservados |
| MAC/IP | Implantado e verificado pelo proxy público | Relatórios assinados, AES-GCM com chave existente, IP observado na borda e mascarado no painel, X-Forwarded-For forjado ignorado; oito interfaces e retenção de 30 dias |
| Capacidade | 500/1000 sessões medidas; variabilidade de CI registrada abaixo | 114000 HTTP 200 na segunda tentativa da CI e 114000 no ensaio local do mesmo código; primeira tentativa teve 1284 timeouts no soak. Não há SLA de 400 req/s nem homologação de carga no host público |
| Cliente e distribuição | Candidato **1.1.2 aprovado na CI**; release geral pendente | Commit `187b726`, CI `33994557511`, artifact `9977794150`; contrato, encerramento, ponte nativa, preservação e pacote passaram. A CI anterior `33994292849` foi cancelada automaticamente pelo último ajuste. O 1.1.1 permanece como evidência histórica |
| Servidor e homologação | **Servidor implantado; Windows real pendente** | Release `es-suite-34e31f2-20260905`, schema 025, três serviços saudáveis, provas reais em loopback/HTTPS e rollback exercitado; [execução no PC](HANDOFF-PC-PRODUCAO-ES-20260905.md) |

**Código e artefatos efetivos.** O commit servidor `34e31f2` incorpora os reparos
de produção `bf4646e`, `050a95a` e `33130d9` da branch Suite/vendas: aviso WhatsApp
na abertura, idempotência por sessão e resolução da compra pública. O consumo
exato do desafio e a regressão que proíbe aviso por heartbeat foram preservados.
O checkout original de produção permanece no commit `33130d9`; a implantação usa
os binários de CI em um novo diretório, sem sobrescrever esse checkout.

- [CI ES servidor 33989933344, tentativa 2 — sucesso](https://github.com/luziellacerda/Servidor-pix/actions/runs/33989933344).
- [Regressão geral servidor 33989933336 — sucesso](https://github.com/luziellacerda/Servidor-pix/actions/runs/33989933336).
- [CI Windows 1.1.1 33989089244 — sucesso](https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33989089244).
- [Artifact servidor 9976783059](https://github.com/luziellacerda/Servidor-pix/actions/runs/33989933344/artifacts/9976783059), 2861977 bytes, ZIP SHA-256 `1a299b048ccf654ca673f2c0ba3e123e0b5587073476938b05aade3c98d769e1`.
- Pacote servidor: API, admin, PIX/painel, migrations 001–025, documentação, evidências e 58 hashes internos conferidos. `COMMIT.txt` corresponde a `34e31f26b6a864a7aa5d701b94fe29ad166e86ac`. ES desabilitado por padrão no pacote e habilitado explicitamente no destino.
- [Artifact Windows 9976171533](https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33989089244/artifacts/9976171533), ZIP externo SHA-256 `6d690fe76d00bb7f5e432e002077abcb6028850f512e85efcd2edaeb4ec70246`. Os três arquivos internos, seus hashes e a instalação estão no [handoff solicitado para o PC](HANDOFF-PC-PRODUCAO-ES-20260905.md#pacote-exato-para-o-pc).

**Carga do código implantado — CI, tentativa 2.** .NET 8.0.30, dois processadores
lógicos, API e gerador no mesmo processo, PostgreSQL real, NAT único, RSA máquina
2048 e autoridade 3072 bits. As 500 e 1000 sessões abriram; a tabela conta as
requisições das fases medidas, sem incluir as aberturas.

| Sessões | Fase | HTTP 200 | Duração (s) | Req/s | p50 (ms) | p95 (ms) | p99 (ms) | Erros |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 500 | Contínua | 12000 | 60,031 | 199,9 | 19,658 | 428,19 | 545,506 | 0 |
| 500 | Rajada | 1000 | 4,893 | 204,36 | 2120,971 | 2448,2 | 2636,056 | 0 |
| 500 | Reconexão | 1000 | 4,765 | 209,88 | 2059,716 | 2428,261 | 2449,138 | 0 |
| 1000 | Contínua | 24000 | 113,773 | 210,95 | 4252,705 | 5562,308 | 5720,176 | 0 |
| 1000 | Rajada | 2000 | 9,669 | 206,85 | 2930,727 | 4184,866 | 4233,639 | 0 |
| 1000 | Reconexão | 2000 | 9,49 | 210,75 | 3308,595 | 4364,249 | 4526,615 | 0 |
| 1000 | Soak | 72000 | 343,524 | 209,59 | 4349,698 | 5613,176 | 5780,642 | 0 |

Total 114000 HTTP 200, zero falhas nesta tentativa. Soak: 209,59 req/s e 343,524 s
para 36 renovações por sessão; não sustentou 400 req/s. Pico global do processo:
220 MiB de working set e 536 MiB privados. O heartbeat continua em cinco segundos;
a execução pode atrasar quando o gerador/API satura os dois núcleos.

**Falha preservada da primeira tentativa do mesmo commit.** O run `33989933344`,
tentativa 1, aprovou os testes funcionais, navegador, carga 500 e as primeiras
fases 1000, mas falhou no soak com **1284 HTTP 504 `REQUEST_TIMEOUT`** (644 na
emissão do desafio e 640 na conclusão da sessão). O diagnóstico foi conservado
no artifact `9976527531` e em `outputs/es-deployment/failed-34e31f2-diagnostics/`.
A segunda tentativa e o ensaio independente abaixo passaram com o mesmo código.
Não foi demonstrada uma causa única desses timeouts nem uma correção específica;
a falha não foi apagada do aceite de capacidade. Timeout, heartbeat, assinaturas
e critérios de falha não foram relaxados para obter aprovação.

**Carga local independente do mesmo código `34e31f2`.** Banco sintético
`es_continuation_ci` em PostgreSQL 16 descartável, limitado a 2 CPUs/1 GiB;
API/gerador fixados em dois núcleos. Não foi usada a base de produção para carga.

| Sessões | Fase | HTTP 200 | Duração (s) | Req/s | p50 (ms) | p95 (ms) | p99 (ms) | Erros |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 500 | Contínua | 12000 | 60,007 | 199,98 | 9,504 | 14,736 | 22,1 | 0 |
| 500 | Rajada | 1000 | 3,01 | 332,18 | 1373,948 | 1521,939 | 1671,575 | 0 |
| 500 | Reconexão | 1000 | 2,951 | 338,87 | 1297,826 | 1484,348 | 1500,818 | 0 |
| 1000 | Contínua | 24000 | 65,5 | 366,41 | 1796,568 | 3181,613 | 3390,733 | 0 |
| 1000 | Rajada | 2000 | 5,551 | 360,27 | 2368,481 | 2795,643 | 2810,645 | 0 |
| 1000 | Reconexão | 2000 | 5,372 | 372,3 | 2357,755 | 2759,168 | 2791,863 | 0 |
| 1000 | Soak | 72000 | 194,353 | 370,46 | 2324,911 | 3145,552 | 3481,332 | 0 |

Total 114000 HTTP 200, zero falhas. Soak: 370,46 req/s em 194,353 s; pico do
processo 201 MiB de working set/261 MiB privados, até oito conexões ativas e duas
esperas por lock. O consumo ES permaneceu com busca pontual pelo índice
`ix_suite_es_challenges_text_lookup`, confirmado por EXPLAIN. O consumo Suite
permanece serializável, com seleção da chave primária seguida de atualização
somente da linha selecionada. API/admin mantêm pool padrão 8 e respeitam limites
explícitos. Container sintético parado e processos de carga encerrados.

As medições de carga não incluem internet, proxy público, TLS ou CNG Windows e
não atestam capacidade do destino. Os resultados históricos `cbdcda9`/cliente
1.1.0 continuam no [registro anterior da matriz](https://github.com/luziellacerda/Servidor-pix/blob/5c2bd059733248557022483149dc09b99a3e3c24/docs/suite/HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md#04-matriz-unica-de-execucao)
e nos arquivos históricos `outputs/es-evidencias-cbdcda9.zip`; foram substituídos
pelos candidatos acima, sem reaproveitar suas medições como se fossem novas.

**Implantação efetiva e preservação do ambiente.** A autenticação nativa `pkexec`
superou o bloqueio inicial de privilégios. Backup PostgreSQL consistente, catálogo
de restauração, configuração protegida e estado PIX foram conferidos antes de
cada troca. As migrations 022–025 foram aplicadas uma vez, às 18:02:39 UTC−3;
as retomadas conferiram seus hashes e conservaram o schema.

Release `/opt/turborama-suite-r5-releases/es-suite-34e31f2-20260905`:

| Serviço | SHA-256 do DLL efetivamente carregado |
| --- | --- |
| `turborama-suite-api.service` | `8a8a90c9a623c155dabeeb3ba88ef0c983964bd603aa3b0240dd1df5fee4c568` |
| `turborama-suite-admin.service` | `fdb7f4914218676fe5bfee7dd4b131bf9f0333a74a68389571e9f37107dfb1d3` |
| `turborama-pix.service` | `f84f3d288f94acb95d957b78e47c71f2903ef1edf5fd840f85deb5a67c4e0b06` |

Drop-in de cada serviço: `zzzz-es-suite-20260905.conf`. Diretórios de trabalho,
estado PIX, conteúdo, credenciais e demais configurações existentes preservados.
Flags efetivas: `Suite__Enabled=true`, `Suite__EmulationStation__Enabled=true`,
`Suite__Inventory__Enabled=true`, `Suite__NetworkInventory__Enabled=true` e retenção
30 dias; a chave AES existente foi carregada por credencial systemd. Dois campos
do inventário anterior foram decifrados com sucesso sem divulgar seu conteúdo.

O nginx/cloudflared existente já encaminhava `/v1/suite/` e normalizava o IP da
borda; nenhuma alteração de proxy foi necessária. Verificação em
`https://app.lzgames.com.br/` e loopback: cabeçalho inválido recebe
`CLIENT_SCOPE_INVALID`, Suite original e ES compartilhado/dedicado alcançam os
contratos corretos, cabeçalho ES é recusado nas rotas de rede. O pin TLS e a
chave pública online coincidem com o cliente 1.1.1 aprovado.

A verificação criptográfica no destino criou somente duas identidades aleatórias
de teste e confirmou abertura/heartbeat de Suite+ES, conflito assinado sem tempo
de acesso, replay/downgrade negados, relatório de rede e máscaras no admin.
O IP observado coincidiu com a borda e não com o X-Forwarded-For forjado.
Revogação sem controles de POST foi negada; revogação exata retornou `REVOKED` e
`ALREADY_REVOKED`, produziu uma auditoria e invalidou o desafio pendente. Uma nova
prova assinada para renovar a sessão revogada recebeu `SESSION_INVALID`; Suite,
ES do cliente B e legado dedicado continuaram funcionando. O teste interno BFF
não substitui a digitação de senha/CSRF em navegador real. As duas identidades
e seus marcadores `SKIPPED` foram removidos; nenhuma notificação de cliente foi
enfileirada na verificação final. Auditoria de segurança conservada.

**Retomadas e rollback.** Três trocas anteriores restauraram automaticamente os
serviços antigos, saudáveis sobre o schema 025. A primeira encontrou bloqueio
HTTP 403 do User-Agent padrão Python; a sonda passou com identificador próprio.
A segunda tinha uma premissa incorreta: emissão de desafio não equivale a renovar
sessão. A sonda foi corrigida para concluir a prova e verificar a negativa real.
A terceira passou em toda a integração, mas detectou dois eventos de abertura
sintéticos no outbox. Sem vínculo de entrega/compra, esses eventos não podiam ser
arrendados pelo worker nem enviados; foram removidos. O preparo final reserva
somente os dois event keys sintéticos como `SKIPPED`, que permanecem intocados.
Os contadores fixos de notificação dos primeiros relatórios não servem como
medição de outbox; o relatório final mede os registros reais. Nenhum desses
ajustes alterou o pacote do servidor ou enfraqueceu o contrato de autorização.

Backup final protegido, acessível somente no host por root:
`/var/backups/turborama-suite/es-suite-34e31f2-20260905T212345Z`.
Reversão operacional preparada:

```bash
pkexec /var/backups/turborama-suite/es-suite-34e31f2-20260905T212345Z/rollback.sh
```

A reversão remove somente os três drop-ins desta entrega, restaura os executáveis
anteriores e conserva schema/auditoria. Não executar down migrations para reverter
binários. Scripts utilizados: `ops/production/deploy-es-suite-20260905.py` e
`ops/production/es-smoke/`; relatórios sanitizados em `outputs/es-deployment/`.
[Registro verificável da implantação e pós-verificação](evidence/es-deployment-20260905.json).
Backups, ambiente e chaves privadas não integram Git nem os artefatos exportáveis.

**Pendência de Windows real.** A UI `LicenseAccessView.cs` e
`AccessFailurePresentation.cs` foi publicada em `ada4555` e está incorporada ao
candidato 1.1.1; o trabalho de interface foi preservado. O operador relatou
“o servidor não retornou uma confirmação válida” antes da implantação. Esse
texto indica resposta/assinatura/contrato não confirmados e não demonstra
revogação de licença. Foi solicitada nova abertura do mesmo EXE/ativação, seguida
de coexistência, heartbeat, revogação pelo painel, áudio/jogos/memória e rede.
O [handoff para o PC](HANDOFF-PC-PRODUCAO-ES-20260905.md) foi solicitado expressamente
pelo operador e publicado nesta mesma entrega. Homologação CNG/Windows e release
geral permanecem pendentes até obter e avaliar o resultado real.

### 0.5 Responsabilidade pelo cliente e pela compilacao

O mesmo executor deve conduzir servidor e cliente compativel ate a entrega,
usando os repositorios autorizados abaixo. Delegacao tecnica e permitida, mas
nao transfere ao usuario a obrigacao de escrever outro handoff para o cliente.

- Servidor: `luziellacerda/Servidor-pix`, branch
  `codex/emulationstation-suite-v1-20260905`.
- Cliente: `luziellacerda/Backup-Instaladores-Compiladores-Turborama`, branch
  `CLIENTE-SUITE-ATIVADO-v1.0.0-20260905`, pasta `TurboramaEmulationStation`.
- Workflow servidor: `.github/workflows/emulationstation-suite.yml`, junto das
  regressoes Suite/PIX existentes. Workflow Windows do cliente:
  `.github/workflows/compilar-cliente-suite-windows.yml`.
- Referencia da Suite, somente para preservar seu contrato:
  `luziellacerda/TRUBORAMA-SUITE`, branch `codex/v2.0.2-music-cleanup-final`.

Inspecionar o HEAD atual de cada branch e preservar os reparos acumulados.
No cliente ha trabalho de interface ja preparado localmente em `LicenseForm.cs`,
`LicenseAccessView.cs`, `AccessFailurePresentation.cs`, `Program.cs` e testes;
nao assumir que tudo ja foi publicado no Git nem sobrescrever esse trabalho.
Reaproveitar o que estiver disponivel e coordenar arquivos sobrepostos antes de
integrar, sem restaurar uma base antiga ou remover melhorias de memoria/audio.

Implementar o contrato compartilhado e a telemetria em `suite-licensing`,
preservando a ponte nativa, helper embutido, verificacao de integridade, DPAPI,
CNG existente, vida da sessao e retorno normal dos jogos. O novo complemento
de inventario usa contrato/acao adicionais autenticados, nao o cabecalho ES em
rotas Suite v1 fora das duas permitidas. Validar todos os novos bytes/provas
entre cliente e servidor com fixtures sinteticas.

Usar nova versao identificavel, tags e arquivos exclusivos da edicao Suite;
nao substituir releases PIX ou do cliente sem servicos independente. Compilar
no GitHub com o workflow proprio, sem exigir instalacao de compilador no PC do
usuario. Preservar os testes `Test-SuiteClientPreservation.ps1`,
`Test-SuiteNative.ps1`, `Test-SuitePackage.ps1` e `suite-licensing/Build.ps1`;
acrescentar as regressoes do contrato novo. Publicar a release para uso somente
apos confirmar compatibilidade do servidor de destino. Se esse destino ainda
estiver bloqueado, entregar o artefato de CI como candidato, nao como release
homologada. Acesso ausente ao repositorio/Windows de homologacao deve constar
como bloqueio especifico; nao apresentar apenas o servidor como entrega total.

## 1. Resultado solicitado pelo usuario

O EmulationStation da edicao sem servicos comerciais deve aceitar o identificador
`TS` que o cliente ja usa no TurboRama Suite e aproveitar a ativacao existente
desse computador. Informar esse identificador e aceitavel. Nao sera solicitado
outro codigo de ativacao, nem criada, substituida ou reativada uma identidade.

O cliente usa a mesma chave CNG existente da mesma conta Windows, e o servidor
confirma que a licenca continua vinculada e autorizada. A Suite e o EmulationStation
precisam poder funcionar juntos. O servidor PIX, pagamentos, locadora e os
demais fluxos existentes devem conservar seu comportamento.

O objetivo da implementacao e transportar as requisicoes ES pelos dois caminhos
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

Todos esses complementos pertencem a entrega obrigatoria da secao 0. Separar
modulos e commits do dispatcher e uma pratica de engenharia, nao permissao para
adiar painel, encerramento, capacidade ou telemetria para outro handoff.

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

Esses resultados correspondem a base acima. **Nao validam a implementacao das
rotas compartilhadas exigida por esta ordem**. Tambem nao
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

## 4. Contrato obrigatorio para as rotas compartilhadas

### 4.1 Caminhos e cabecalho de despacho

Implementar no cliente ES o envio do mesmo corpo Suite v1 aos caminhos:

```text
POST /v1/suite/challenges
POST /v1/suite/sessions
X-TurboRama-Client: EMULATIONSTATION
```

O cabecalho e somente uma indicacao de qual implementacao atendera a requisicao.
A autorizacao continuara dependendo da prova RSA-PSS da chave cadastrada, da
licenca, do dispositivo, do contexto, do desafio e das verificacoes transacionais.
Conhecer o nome do cabecalho nao concede acesso.

Regras obrigatorias para o dispatcher:

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

Codigo de erro para cabecalho/escopo invalido: HTTP 400,
`CLIENT_SCOPE_INVALID`, com mensagem publica generica. Esse novo codigo deve
ser implementado, documentado e coberto pelos testes nesta entrega.

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

| Resposta | Kind obrigatorio |
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

A separacao decorre do Kind dentro do payload assinado, de sua
validacao estrita pelo cliente e das tabelas isoladas de desafios/sessoes. Nao
e autorizada rotacao de dominio, produto, chave ou autoridade por esta ordem.

Implementacao minima sugerida: criar um wrapper de `IAssertionSigner`, usado
exclusivamente pelo servico ES compartilhado. Ele reconhece as quatro respostas
de abertura/heartbeat, troca o campo `Kind` do record antes da assinatura e
delega ao signer existente. Deve rejeitar tipos/acoes inesperados. Nunca registrar
esse wrapper como substituto global do signer da Suite ou dos endpoints legados.

O `RsaAssertionSigner` existente serializa o record canonico, assina os bytes e
monta o envelope a partir do mesmo record. Essa propriedade precisa ser
preservada; os testes devem verificar a assinatura e ambos os Kind.

### 4.4 Falha segura com servidor antigo ou cabecalho removido

O cliente compartilhado deve validar o Kind ES esperado **antes de assinar o desafio
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

Ambas as entradas ES, dedicada e compartilhada, podem usar a mesma persistencia
ES: continua existindo uma sessao ES por licenca/equipamento. O caminho dedicado
legado conserva sua substituicao; o compartilhado passa a seguir a mesma politica
de nova abertura validada da Suite conforme a decisao revisada da secao 0.3.
Isso nao une os namespaces: desafios SHARED_V1 e DEDICATED_V1 continuam isolados.
Implementar essa politica no adapter/store correto, sem campo arbitrario do
cliente que dispense prova ou elegibilidade. Nenhum caminho ES pode substituir
a sessao Suite, que continua independente.

## 5. Limites de alteracao no servidor

Alteracoes obrigatorias no modulo de rotas compartilhadas:

1. Parser estrito do cabecalho e decisao de despacho nas duas rotas existentes.
2. Adapter/servico ES compartilhado usando o `IEmulationStationStore` existente.
3. Wrapper de signer limitado aos quatro Kind ES novos.
4. Rejeicao do cabecalho em endpoints de operacao fora desse contrato.
5. Testes, documentacao do contrato e atualizacao do workflow de verificacao.

Esta lista limita o modulo de despacho, nao o escopo total da ordem. Tambem
implementar as alteracoes localizadas necessarias em painel/BFF/backend
administrativo, persistencia, telemetria e cliente conforme 0.2 e 6.3–6.6.
Migrations adicionais devem ser aditivas e revisadas; nao deixar essas frentes
pendentes sob a justificativa de que o diff deve conter somente dispatcher.

Manter `SuiteService.cs`, `Protocol.cs` e `Store.cs` originais sem mudancas de
logica sempre que possivel. Nao alterar as constantes Kind globais para que
todos os clientes passem a receber Kind ES. O wrapper deve ser local ao novo
caminho compartilhado.

Para o despacho HTTP compartilhado, a migration 022 existente ja oferece as tabelas necessarias.
Nao ha necessidade prevista de nova migration, nova tabela de licencas,
duplicacao de cadastro, troca de produto, nova ativacao ou novo registro CNG.
Se a implementacao revelar necessidade de schema adicional, registrar a razao
e revisar o contrato e a migration dentro desta mesma execucao antes de alterar
o banco. Nao exigir um novo documento do usuario para uma migration aditiva
necessaria aos requisitos ja autorizados.

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
`SuiteAdminBff.cs` dentro do projeto `TurboRamaPixOnlineServer`. A extensao
obrigatoria da area administrativa Suite nesses componentes deve ser revisada como
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
- Kind Suite antigo apresentado ao cliente compartilhado: rejeicao antes
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
Se esse fluxo de conta autenticada nao existir, registrar a lacuna e implementar
nesta entrega a operacao restrita ao administrador autorizado, conforme 0.3.
Reutilizar recuperacao Suite ja aprovada quando aplicavel, sem criar outra.
Nao inventar autenticacao de titular com base somente no ledger,
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

Implementar o contrato adicional de encerramento abaixo. O executor define os
nomes/DTOs/versao e testes concretos na mesma entrega, sem alterar o contrato v1:

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

A decisao revisada da secao 0.3 manda `session.open` validado substituir a sessao
ES anterior, como a Suite. A confirmacao administrativa e uma operacao separada,
nao requisito da reabertura normal. Testar a substituicao imediata e a recusa do
heartbeat antigo junto do ES 1.0.1; nao alterar o fluxo normal da Suite nem a
compatibilidade da secao 4.5. Uma confirmacao administrativa capturada antes da
troca jamais pode revogar a nova sessao.

Testes obrigatorios: titular autorizado, conta divergente, identificador TS
sozinho, administrador sem claim, CSRF/step-up ausentes, prova trocada, replay,
alvo de outro cliente/aplicacao, confirmacao antiga, abertura concorrente,
heartbeat apos revogacao e manutencao das restricoes financeiras. Encerrar A
nunca deve afetar a licenca, a sessao nova ou as sessoes de B.

### 6.4 Painel: qual programa tem uma sessao autorizada

**Entrega obrigatoria nas paginas de gerenciamento existentes.** O usuario
reforcou que ja existem sites/paginas para gerenciar esses dados e que as novas
informacoes precisam aparecer neles. Nao criar outro site, outro login ou um
dashboard separado. Nao considerar esta frente entregue somente com tabelas,
endpoints, JSON, logs ou campos que a interface nunca consulta.

Pontos de integracao verificados no codigo da base `db4a992`:

| Pagina existente | Alteracao obrigatoria |
| --- | --- |
| `/admin`, bloco `#suite-clients` (CLIENTES SUITE / Conexoes do programa) | Mostrar presenca Suite e ES separadas, resumo por cliente/computador, estado e ultimo contato; manter acesso ao historico do cliente |
| `/admin/fragments/suite-clients` | Atualizar o mesmo bloco com consulta autorizada e paginada/batch; nao listar todo o cadastro e consultar cada cliente separadamente a cada refresh |
| `/admin/clientes/{licenseId}` (Historico do cliente) | Integrar sessoes por aplicacao e complemento de rede, detalhes mascarados/autorizados e botao de encerramento da sessao ES selecionada; preservar placa-mae, baseline, downloads e historico existentes |
| `/admin/suite`, inclusive selecao por `licenseId` | Manter resumo, compras/liberacoes e links coerentes com as sessoes Suite/ES; nao confundir estado financeiro com presenca online |

Arquivos a inspecionar e alterar de forma localizada:

- `src/TurboRamaPixOnlineServer/AdminPanel.cs`: `/admin`, fragmento, navegacao e
  atualizacao automatica do bloco de clientes Suite.
- `src/TurboRamaPixOnlineServer/SuiteAdminPanel.cs`: `ActiveCustomerPanelAsync`,
  `CustomerHistoryPage`, `Page` e `CustomerLicenseList`.
- `src/TurboRamaPixOnlineServer/SuiteAdminBff.cs`: DTOs e chamadas administrativas.
- `src/TurboRamaSuiteAdminServer/Program.cs`: dados administrativos, atualmente
  incluindo `/customer-activity/{id}` e consultas de sessao Suite.

O backend atual consulta sessoes Suite e a presenca antiga nao distingue ES;
implementar o suporte real a ambas as aplicacoes, com DTO/consulta aditivos e
autorizacao verificada no servidor. Nao somente trocar o texto "programa" no HTML.
O `AgentVersion` atualmente exibido como programa nao comprova qual aplicacao
possui sessao; `SessionId` nao vazio nao comprova que ela ainda esta online.

Apresentar ao administrador autorizado, sem remover as informacoes atuais:

- cliente/cadastro comercial e a natureza comprovada de seu vinculo, licenca
  mascarada, computador/dispositivo vinculado e aplicacao fixa Suite ou ES;
- estado da sessao, ultimo contato autenticado, validade/expiracao e identificador
  mascarado da instancia selecionada;
- IP observado pelo servidor e MACs informados pelo cliente, mascarados na
  listagem, origem do dado e instante de coleta; detalhes adicionais somente
  com permissao especifica e sem vazar valores em pagina/endpoint nao autorizado;
- aviso claro **"IP apenas informativo: mudanca de rede nao bloqueia acesso"**;
- estado "nao informado por esta versao" para clientes antigos/sem telemetria,
  sem transformar ausencia de dados em falha de licenciamento;
- acao **"Encerrar sessao EmulationStation"** na linha correta, confirmacao
  identificando cliente/computador/aplicacao/alvo e resultado coerente apos
  atualizar. Nao oferecer encerramento em massa por IP nem misturar com suspensao
  comercial ou encerramento da sessao Suite.

A listagem atual percorre os clientes e chama `CustomerActivityAsync` por linha,
com refresh do fragmento a cada tres segundos. Corrigir esse N+1 para os dados
desta entrega e limitar a frequencia/paginacao/carga conforme 6.5. Preservar
selecao e foco durante refresh, inclusive quando uma confirmacao estiver aberta;
nao permitir que uma linha atualizada troque silenciosamente o alvo confirmado.

As rotas `/admin` e seu fragmento hoje exigem autenticacao geral; isso nao
substitui as permissoes especificas de ler dados sensiveis e encerrar sessoes.
Aplicar os controles tambem no fragmento, detalhes e endpoints, nao apenas
ocultando botoes. Reutilizar a fronteira administrativa com ator/claims e
CSRF/step-up descrita abaixo, sem presumir que token interno sozinho comprova
titularidade. Manter navegacao, estilos, historico e controles existentes.

Aceite visual e funcional nesta mesma execucao: testar as tres paginas e o
fragmento com dois clientes sinteticos e Suite/ES simultaneos; verificar desktop
e tela estreita, filtros, paginacao, contadores, dados ausentes, carregamento,
erro de backend e permissao negada. Encerrar ES do cliente A deve atualizar
somente o alvo; Suite de A e ambas as sessoes de B permanecem normais. Registrar
evidencias com dados sinteticos/mascarados, nunca captura com licenca real,
credencial ou rede real de cliente. Os valores precisam vir da API implementada,
nao de mock permanente usado para simular uma entrega pronta.

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

MAC da placa de rede nao atravessa uma requisicao HTTP pela internet. Implementar
no cliente correspondente o envio explicito do sinal coletado localmente e no
servidor sua validacao/persistencia/exibicao autorizada, todos nesta entrega.
Limitar a coleta a interfaces relevantes ativas/fisicas e no maximo oito
interfaces, validado no cliente e no servidor. Definir forma
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

## 7. Implantacao e homologacao integrantes da ordem

A documentacao historica aponta `turborama-suite-api.service` e o listener Suite
`127.0.0.1:5190`. Ela cita releases sob `/opt/turborama-suite-r5-releases/`. Isso
e referencia para verificacao, nao prova do destino atual. Este arquivo nao
contem credenciais. Usar somente o destino e o acesso operacional ja autorizados
ao executor, conforme 0.1; nao deduzir permissao de um nome historico de unidade.
Sem esse acesso, terminar codigo/CI/artefatos e registrar o bloqueio operacional
exato. Nao encerrar antes apenas por haver uma etapa de implantacao restante.

### 7.1 Confirmar o processo e a diferenca entre origem e acesso publico

No host autorizado, consultar metadados da unidade sem imprimir
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

Para as rotas compartilhadas implementadas, testar tambem a propagacao exata do
cabecalho. Um servidor antigo pode devolver o mesmo erro de JSON para `{}` com
ou sem cabecalho; esse teste sozinho nao prova suporte ao novo contrato. A prova
de suporte exige testar os Kind novos com identidade sintetica no ambiente
apropriado ou com cliente autorizado na homologacao.

### 7.2 Preparar e habilitar a implementacao aprovada

Executar apos implementar, revisar e aprovar os testes do contrato nesta mesma
tarefa, e confirmar o destino e a autoridade operacional conforme 0.1:

1. Identificar o commit/binario e a configuracao realmente carregados no host.
2. Preservar o release anterior e a configuracao para rollback.
3. Confirmar migrations 001–021. Se 022 ainda estiver ausente, aplicar
   `migrations/suite/022_suite_emulationstation_sessions.up.sql`, uma vez, na
   conexao Suite previamente confirmada. Exemplo de comando, sem credenciais:

   ```bash
   psql --set=ON_ERROR_STOP=1 \
     --file=migrations/suite/022_suite_emulationstation_sessions.up.sql
   ```

   Aplicar tambem, em ordem e uma unica vez, as migrations aditivas efetivamente
   implementadas/testadas para sessoes administrativas e telemetria desta ordem.
   Conferir checksums/grants e banco alvo; nao reaplicar migrations existentes,
   executar down destrutivo ou tratar o exemplo da 022 como instalacao completa.

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

Publicar tambem os componentes realmente alterados de listagem, encerramento e
telemetria (backend administrativo, painel/BFF e cliente), com passos por
componente dentro desta mesma execucao. O roteiro de reiniciar somente a API nao
instala por si uma funcionalidade nova nas paginas de gerenciamento. Confirmar
as unidades/alvos efetivos e a janela operacional autorizada antes de reiniciar
o componente hospedeiro do painel; preservar os servicos PIX que ele hospeda.
Revisoes por componente nao sao pedidos para gerar outra ordem ou outro handoff.
Nao ampliar acesso/paradas alem do destino e escopo autorizados.

Nao usar `ops/deploy-round12.sh` como instalador Suite: aquele script e do PIX e
opera outra aplicacao. O workflow ES atual gera artefato de revisao e executa
testes; nao deve receber deploy automatico como efeito colateral deste trabalho.

### 7.3 Rollback

Desabilitar `Suite__EmulationStation__Enabled` e, se necessario, voltar o
apontamento da API Suite ao release anterior, preservando sua configuracao.
Revalidar Suite e servicos existentes. A indisponibilidade ES deve ser apresentada
como tal ao cliente, sem fallback para consumir a sessao original da Suite.

A migration 022 e as demais migrations aditivas compativeis podem permanecer
instaladas; nao e necessario apagar tabelas ou registros para voltar ao binario
anterior. Testar essa compatibilidade antes da publicacao. Nao executar down migrations
destrutivas como rotina de rollback. Nao restaurar chaves ou reativar licencas
como forma de contornar uma falha de roteamento.

## 8. Criterios obrigatorios de conclusao conjunta

O executor responde pelo conjunto servidor, paginas e cliente. Os criterios
abaixo pertencem a uma unica entrega; nao sao solicitacoes de novo handoff.
Antes de anunciar a versao como homologada, apresentar evidencias de todos eles:

1. **Contrato confirmado:** caminhos, cabecalho exato, quatro Kind, compatibilidade
   e comportamento de falha revisados com a equipe frontend.
2. **Implementacao revisavel:** commits de dispatcher, adapter/signer, persistencia,
   autorizacao, painel/BFF, telemetria, cliente e testes, com limites claros por
   modulo. Demonstrar que o caminho Suite sem cabecalho, o PIX, a identidade e
   os reparos de audio/memoria/jogos do cliente permaneceram preservados.
3. **CI aprovado:** testes de protocolo, HTTP, compatibilidade e PostgreSQL real
   para os commits finais, incluindo servidor antigo/cabecalho removido, todas
   as novas frentes e compilacao Windows do cliente. CI historico nao conta.
4. **Artefato identificado:** commit e SHA-256 do pacote/binario, configuracao
   padrao desabilitada e procedimento de rollback correspondente.
5. **Destino confirmado:** processo/binario, listener, migrations aplicaveis, flag e
   encaminhamento efetivos verificados no ambiente autorizado, sem divulgar
   credenciais nem dados privados.
6. **Homologacao conjunta:** cliente compartilhado reconhece os Kind ES e abre
   a sessao isolada com a ativacao existente; Suite e ES continuam autorizados
   simultaneamente; ES 1.0.1 permanece compativel.
7. **Titularidade e painel:** lacuna de conta autenticada resolvida por vinculo
   confiavel ou operacao restrita a administrador; sessoes separadas por
   aplicacao, permissoes/CSRF/step-up verificados, encerramento com confirmacao
   e alvo exato, sem afetar sessoes novas, Suite, PIX ou outros clientes.
   Validar as paginas existentes com dados sinteticos: informacoes visiveis,
   filtros/paginacao, detalhes autorizados, confirmacao e estados de erro/vazio.
   API pronta sem a informacao e a acao nas paginas nao satisfaz este criterio.
8. **Escala medida:** testes de 500/1000 sessoes, NAT, rajada/soak, pool/locks e
   latencias registrados; isolamento entre clientes e limites aprovados para
   o host medido. Nao substituir evidencia por promessa de capacidade.
9. **Sinais complementares:** contrato MAC/IP separado e aditivo, origem IP
   confiavel, coleta limitada, privacidade/retencao/testes definidos e nenhuma
   alteracao de inventario, fingerprint, autoridade ou vinculo existentes.
   IP e estritamente informativo: nenhuma mudanca/valor/divergencia pode provocar
   bloqueio direto ou indireto, encerramento, negacao de reconexao ou reativacao.
   Entregar e validar o coletor no frontend novo; endpoint vazio ou campo
   ficticio no painel nao e evidencia de coleta implementada.
10. **Liberacao coordenada:** comunicar o commit/artefato do servidor validado e
   a disponibilidade do contrato. So entao liberar a versao frontend correspondente.

O frontend deve manter a exigencia da chave CNG existente na mesma conta Windows,
validar rigorosamente os novos Kind antes de produzir a prova, usar somente o
identificador ja ativado e explicar indisponibilidade do servidor sem pedir nova
ativacao. Este handoff nao autoriza enfraquecer essas verificacoes para que um
cliente novo funcione contra um servidor antigo.

## 9. Historico e formato de encerramento da execucao

Historico: `f1bb86a` publicou somente a orientacao original; `db4a992` registrou
somente sua leitura/retorno. Nenhum deles implementou as rotas compartilhadas,
o novo painel, encerramento ou telemetria. Esta revisao corrige a ordem para
execucao integral; nao altera retroativamente esses fatos nem comprova deploy.

A execucao so pode ser declarada concluida apos preencher a matriz da secao 0.4
e satisfazer a secao 8. O resumo final deve conter, em uma unica entrega:

1. O que foi efetivamente implementado em servidor, paginas e cliente, com commits.
2. Quais testes/CI passaram para esses commits, incluindo carga medida e limites.
3. Links dos artefatos/releases separados, versoes e hashes SHA-256.
4. Quais componentes foram realmente implantados, onde, e qual verificacao
   confirmou funcionamento conjunto sem alterar Suite/PIX e outros clientes.
5. Qualquer bloqueio externo ainda existente, com evidencia e acao minima para
   resolve-lo. Distinguir "pronto para implantar" de "implantado e homologado".

Nao produzir outro arquivo de recebimento como resultado final. Atualizar este
documento e a documentacao tecnica dos componentes conforme o codigo entregue.
Nao declarar completo um item apenas planejado, compilado sem teste aplicavel,
simulado sem ambiente real, ou dependente de um cliente ainda nao implementado.
