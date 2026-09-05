# Ordem unica de execucao completa: EmulationStation integrado a Suite

Data: 2026-09-05.

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
- **Concorrencia ES:** o cliente compartilhado deve receber conflito verificavel
  quando houver uma sessao ES vigente, sem encerra-la silenciosamente. Implementar
  confirmacao pelo caminho autorizado. Somente a mesma instancia ainda em uso
  renova sua propria sessao, mantida em memoria, com prova CNG e validacao do
  servidor; nao compartilhar sessao entre processos usando cache como atalho.
  Cache de identificador DPAPI nao e autorizacao. Uma sessao expirada permite
  nova abertura normal, nunca o reaproveitamento da sessao vencida. Preservar o cliente
  dedicado ES 1.0.1 conforme 4.5, deixando clara a limitacao de substituicao desse
  legado; nao afirmar que clientes antigos ja exigem a nova confirmacao.
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

Atualização do executor em 2026-09-05: servidor, painel e cliente implementados;
testes locais e CIs finais aprovados; candidatos do servidor e Windows baixados
e hashes conferidos. Implantação e homologação no Windows real permanecem
pendentes pelos bloqueios abaixo.

| Frente | Estado verificado | Evidência |
| --- | --- | --- |
| Rotas/licenciamento compartilhado | Implementado; HTTP/PostgreSQL e assinaturas passaram | Servidor `cbdcda97bab360c4c248da0959f53ff782edcacf`; cliente `18b464a34ab9fffe2f222776b248164e4a6f6159`; cabeçalho estrito, quatro Kind, conflito assinado sem autorização, anti-replay, Suite sem cabeçalho e legado dedicado |
| Titularidade, painel e encerramento | Implementado no painel existente; testes funcionais e navegador aprovados | Papéis PostgreSQL restritos; token/claims/CSRF/step-up/CAS/replay/A-B; páginas HTTPS em Chromium a 1440×1000 e 390×844, filtros, paginação, confirmação preservada, XSS, rótulos Suite/ES e revogação do alvo exato |
| MAC/IP no servidor e cliente | Implementado e testado | Contrato idêntico nos repositórios (SHA-256 `54855f4a2a4fc5573da6dc8f75f8ad1f4a5802ab1de797829a851182107890a9`); provas, AES-GCM/tamper, proxy não confiável ignorado, IPv6 encaminhado por proxy confiável, máscaras, limite de oito interfaces, retenção e relatório mascarado no painel |
| Capacidade e isolamento | CI 500/1000: 114 mil requisições sem erros; local 1000: 100 mil sem erros | NAT único, duas aplicações por computador, abertura, heartbeat, rajada, reconexão e soak; consumo pelo desafio exato, pools padrão API 8/admin 8 e retries limitados ao timeout HTTP |
| Cliente e artefatos GitHub | Candidatos cliente 1.1.0 e servidor gerados; hashes conferidos | [CI Windows 33982373836 — sucesso](https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33982373836); [CI servidor 33987322471 — sucesso](https://github.com/luziellacerda/Servidor-pix/actions/runs/33987322471); [regressão geral servidor 33987322497 — sucesso](https://github.com/luziellacerda/Servidor-pix/actions/runs/33987322497) |
| Implantação e homologação | Bloqueadas; nenhum componente implantado por esta execução | Serviço efetivo continua em `r25-7-whatsapp-session-open-20260903`; diretórios de release e drop-in systemd não graváveis; sudo exige senha; executor permitido sem namespace disponível; PC Windows com ativação existente não disponível |

**Carga local final do código `cbdcda9`.** 1000 sessões, 500 computadores sintéticos
com Suite+ES, gerador e API no mesmo processo limitado a dois núcleos, .NET 8.0.30,
PostgreSQL 16 limitado a 2 CPUs/1 GiB, RSA máquina 2048 e autoridade 3072 bits.
Todas as 1000 aberturas passaram; a tabela conta as requisições das fases medidas.

| Fase | Requisições | Duração (s) | Req/s | p50 (ms) | p95 (ms) | p99 (ms) | Erros |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Contínua | 24000 | 65,912 | 364,120 | 2147,246 | 3150,360 | 3298,590 | 0 |
| Rajada | 2000 | 5,488 | 364,460 | 2309,600 | 2754,604 | 2775,163 | 0 |
| Reconexão | 2000 | 5,348 | 373,990 | 2299,942 | 2647,136 | 2665,212 | 0 |
| Soak | 72000 | 199,217 | 361,420 | 2388,265 | 3275,417 | 3616,826 | 0 |

Resultado: 100000 respostas HTTP 200 e zero falhas de troca. O soak solicitado de
180 segundos completou em 199,217 segundos, com 361,420 req/s: esse ambiente de
dois núcleos não sustentou a estimativa de 400 req/s incluindo o gerador. Não
alteramos o heartbeat nem a janela de autorização para melhorar a medição.
Pico do processo gerador+API: 203 MiB de working set e 275 MiB privados; até oito
conexões PostgreSQL ativas e três esperas por lock nas amostras. As 78 amostras do
container PostgreSQL, após a abertura até o fim do soak, registraram pico de
229 MiB e 154,91% de uma CPU (quota de duas CPUs). Esses resultados não incluem
internet, proxy público, TLS ou CNG do computador real e não homologam produção.

Microbenchmark separado, sequencial em um núcleo, .NET/OpenSSL Linux, 500 operações
por tamanho e mensagens sintéticas de 1024 bytes: RSA-PSS-SHA256 2048 teve p50 de
assinatura/verificação de 0,7931/0,0344 ms; RSA 3072, 2,8081/0,0903 ms. Não mede
CNG Windows. JSONs completos, fontes do microbenchmark, planos e logs estão nas
evidências locais em `outputs/`.

**Falhas encontradas e corrigidas.** As primeiras cargas expuseram comparações
`text`/`char(64)` sem índice adequado (migration 025), saturação do pool padrão de
100 conexões e conflitos SSI em bancos novos. O consumo Suite agora materializa
pela chave primária e atualiza somente a linha travada (`Tid Scan`), evitando
leituras pelo índice parcial de expiração; permanece serializável e atômico.
As tentativas são limitadas a doze, com jitter e timeout HTTP de dez segundos.
O workflow agora usa `pipefail`: a CI `33985422910` havia ocultado uma falha de
carga ao gravar o log, e depois falhou pela ausência do relatório ao empacotar.
Essas execuções não são contadas como validação final.

**CI final e pacote servidor.** A execução `33987322471`, no commit
`cbdcda97bab360c4c248da0959f53ff782edcacf`, aprovou todos os passos: regressões
Suite/PIX/admin, protocolo/HTTP/PostgreSQL, painel Chromium, carga e empacotamento.
As 500 e depois 1000 sessões abriram sem erros. Medição do runner Linux de dois
núcleos, .NET 8.0.30, com gerador e API no mesmo processo:

| Sessões | Fase | Requisições | Duração (s) | Req/s | p50 (ms) | p95 (ms) | p99 (ms) | Erros |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 500 | Contínua | 12000 | 69,254 | 173,270 | 2453,126 | 3308,538 | 3371,577 | 0 |
| 500 | Rajada | 1000 | 5,632 | 177,550 | 2317,439 | 2826,897 | 2853,334 | 0 |
| 500 | Reconexão | 1000 | 5,616 | 178,070 | 1868,224 | 2503,120 | 2545,566 | 0 |
| 1000 | Contínua | 24000 | 137,186 | 174,950 | 5218,747 | 6715,791 | 6873,143 | 0 |
| 1000 | Rajada | 2000 | 11,426 | 175,040 | 4870,184 | 5855,786 | 5903,459 | 0 |
| 1000 | Reconexão | 2000 | 11,480 | 174,210 | 4929,345 | 5994,715 | 6079,335 | 0 |
| 1000 | Soak | 72000 | 410,047 | 175,590 | 5336,995 | 6789,084 | 6972,944 | 0 |

Total: 114000 respostas HTTP 200 e zero falhas. O runner sustentou aproximadamente
175,59 req/s no soak e não atingiu a estimativa de 400 req/s para 1000 sessões.
O conjunto de 36 renovações por sessão levou 410,047 s; o intervalo configurado
continua cinco segundos, mas a execução ficou limitada pelo ambiente. Pico do
processo: 214 MiB de working set e 665 MiB privados. Esses resultados comprovam
funcionamento sob a carga medida, não um SLA de 400 req/s nem homologação do host
público. Os limites de aceite do destino ainda dependem da medição nesse destino.

[Artefato servidor 9975812219](https://github.com/luziellacerda/Servidor-pix/actions/runs/33987322471/artifacts/9975812219):
API Suite, backend administrativo, PIX/painel, migrations 001–025, documentação,
relatórios JSON e capturas do navegador. `COMMIT.txt` corresponde ao commit acima;
os 58 hashes internos foram conferidos. ES permanece desabilitado por padrão.
O artefato do GitHub está configurado para expirar em 2026-09-19; a cópia local foi
conservada em `outputs/servidor-suite-emulationstation-cbdcda9.zip`.

- ZIP servidor SHA-256: `4ae70941c274ac9e7e3219e1482de09226467a373c7cea43933a9fec65243329`.
- Manifesto `SHA256SUMS.txt` SHA-256: `88f978e67ef031e6cc4e7d0e0145d55d438f58707272908f2c25f1536354bbeb`.

**Artefatos Windows conferidos.** Commit `18b464a34ab9fffe2f222776b248164e4a6f6159`,
versão 1.1.0; [artefato 9974239120](https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33982373836/artifacts/9974239120),
com EXE, ZIP portátil e ZIP de atualização. A release para uso depende da
compatibilidade do destino; nenhum candidato foi publicado como homologado.

- EXE: `2737a8a9fcd1dc28b0c7a4b77064b615c907235da3b8e16520d54fc9d505c117`.
- ZIP portátil: `22b79688fe0592dc7a811dda96975ac90e1eaa6f3aee08866d5330f7867330b6`.
- ZIP de atualização: `0a8328f4161f3b4dcc244a276938056df7345c073bb8b0502737ce43ad564996`.
- Arquivo do artefato GitHub: `9e05a08b08c30ae5e1a3de8e4cc64ea16907701a15baa7be4cf839d26e7c84be`.

**Rollback e bloqueios restantes.** Migrations 001–025 foram aplicadas somente
no PostgreSQL descartável. O binário/testes da base `da18086a4c130798ba67dee8a2a4ac05afcfbe3a`
passou contra o schema 025, verificando compatibilidade com a base anterior;
não foi executado rollback em produção. Fixtures encerradas e container de teste
parado após as medições.

A verificação final de permissões confirmou que `/opt/turborama-suite-r5-releases`
e `/etc/systemd/system/turborama-suite-api.service.d` não são graváveis e
`sudo -n /usr/bin/true` exige senha. A ação mínima pendente é implantar o pacote
revisado por uma conta autorizada a atualizar esses caminhos e operar os serviços,
aplicar somente as migrations faltantes, configurar flags/rotas e homologar com
um PC Windows que já possua a chave CNG/TS Suite. A sonda pública sintética ainda
retornou `JSON_INVALID` ao cabeçalho inválido, sem confirmar o dispatcher novo.
As alterações de interface mencionadas fora do Git (`LicenseAccessView.cs`,
`AccessFailurePresentation.cs`) não estavam disponíveis; o candidato preserva a
base publicada e os testes de memória, áudio, ponte nativa, DPAPI, IPC e pacote.
Não houve alteração de serviços, banco, flags ou binários de produção.

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
legado pode substituir a sessao ES anterior conforme seu contrato atual; o novo
caminho compartilhado deve aplicar o conflito e a confirmacao da secao 0.3.
Implementar essa politica explicitamente no adapter/store correto, com testes,
sem um bypass controlado por campo arbitrario do cliente. Nenhum dos caminhos
ES pode substituir a sessao Suite, que continua independente.

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

A base atual permite que `session.open` substitua a sessao ES anterior. Implementar
a experiencia de conflito/confirmacao para o cliente compartilhado conforme 0.3
e testa-la junto do ES 1.0.1; nao alterar silenciosamente o fluxo normal da Suite
nem eliminar a compatibilidade declarada na secao 4.5. Publicar as limitacoes
reais do legado; nao prometer confirmacao obrigatoria em clientes antigos.

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
