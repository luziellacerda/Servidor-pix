# Handoff de retorno — EmulationStation nas rotas compartilhadas da Suite

Data: 2026-09-05.

Repositório: `luziellacerda/Servidor-pix`.

Branch: `codex/emulationstation-suite-v1-20260905`.

**Estado desta entrega: leitura concluída e retorno documental.** O pedido desta
rodada foi ler o novo handoff e registrar o retorno no Git. Esta entrega não
implementa as funcionalidades propostas nem certifica sua disponibilidade em
produção.

## 1. Referências e verificações realizadas

- Lido integralmente o [handoff recebido](HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md),
  publicado no commit `f1bb86a93631de625e620d1392ce225ced6842af`.
- Lida a [documentação da extensão ES existente](EMULATIONSTATION-INTEGRATION.md).
- Consultados branches, histórico e arquivos alterados no GitHub. O commit
  `f1bb86a` adicionou somente o handoff recebido, com 795 linhas.
- Confirmados na API do GitHub os dois workflows concluídos com `success` para
  a implementação inicial `769f8b44c87b53ec6393276548a61da79b43aa22`:
  [validação do servidor](https://github.com/luziellacerda/Servidor-pix/actions/runs/33974034488)
  e [verificações isoladas ES com PostgreSQL](https://github.com/luziellacerda/Servidor-pix/actions/runs/33974034512).

Os resultados de CI acima pertencem à implementação inicial com rotas dedicadas.
Não são testes novos executados nesta rodada e não validam as rotas compartilhadas
propostas. O estado do binário, da configuração e das migrations em produção não
foi inspecionado nesta rodada.

A `main` consultada ainda aponta para `a3bb0f20da79cc9ffe4779d2ab0526e53c952914`,
de agosto. A continuidade deste trabalho deve usar a branch ES indicada acima.

## 2. Resultado esperado e estado de cada frente

O cliente informa o identificador TS já ativado e usa a chave CNG existente da
mesma conta Windows. Suite e EmulationStation devem permanecer autorizados ao
mesmo tempo, com sessões independentes. Não há nova ativação, recriação de chave
ou descoberta automática de licença por `DeviceId` neste contrato.

| Frente | Estado identificado nas referências | Continuidade necessária |
| --- | --- | --- |
| ES com rotas dedicadas | Implementação inicial em `769f8b4`, tabelas ES da migration 022 e flag desabilitada por padrão; CI aprovado | Confirmar separadamente a instalação e a configuração no destino |
| ES nas rotas Suite existentes | Proposta documentada em `f1bb86a` | Implementar despacho estrito, adapter e signer específicos; validar HTTP e PostgreSQL |
| Sessões por aplicação no painel | Requisito futuro | Listagem autorizada, paginada e com presença derivada de cada sessão |
| Encerramento de sessão anterior | Requisito futuro | Definir titularidade, contrato de confirmação e revogação do alvo exato |
| Centenas de clientes | Plano de teste e estimativa de tráfego | Medir 500/1.000 sessões, inclusive NAT compartilhado, concorrência e carga sustentada |
| Complemento MAC/IP | Requisito futuro e aditivo | Definir contrato, coleta cliente quando necessária, proteção, retenção e testes |

O HTTP 404 da rota ES pública foi relatado no handoff recebido; não foi reproduzido
nesta rodada. Ele não determina sozinho se falta o binário novo ou o encaminhamento
do proxy e não demonstra falha de licença, chave CNG ou dispositivo.

## 3. Contrato técnico compreendido

O transporte compartilhado proposto usa:

```text
POST /v1/suite/challenges
POST /v1/suite/sessions
X-TurboRama-Client: EMULATIONSTATION
```

O cabeçalho seleciona o atendimento. A autorização continua dependendo da prova
criptográfica e das verificações de licença, vínculo, dispositivo e desafio.

- Cabeçalho ausente mantém o fluxo Suite original e seus bytes canônicos.
- Somente uma ocorrência com o valor exato `EMULATIONSTATION` seleciona ES.
  Valores vazios, desconhecidos, duplicados ou CSV são rejeitados com o erro
  proposto `400 / CLIENT_SCOPE_INVALID`, sem desvio para o fluxo Suite.
- O cabeçalho deve ser rejeitado nas operações fora das duas rotas compartilhadas,
  inclusive ativação, conteúdo, inventário e rotas ES dedicadas.
- ES desabilitado responde `503 / EMULATIONSTATION_DISABLED`. As rotas dedicadas
  continuam compatíveis com ES 1.0.1, sem esse cabeçalho e com o signer anterior.
- O adapter compartilhado usa o store ES. O wrapper de assinatura é local a esse
  adapter; não substitui globalmente o signer da Suite ou do ES dedicado.

Os quatro `Kind` propostos devem constar no envelope e no payload canônico antes
da assinatura:

| Resposta | Kind |
| --- | --- |
| Desafio de abertura | `TURBORAMA_SUITE_ES_SESSION_OPEN_CHALLENGE` |
| Desafio de heartbeat | `TURBORAMA_SUITE_ES_SESSION_HEARTBEAT_CHALLENGE` |
| Abertura | `TURBORAMA_SUITE_ES_SESSION_OPEN` |
| Heartbeat | `TURBORAMA_SUITE_ES_SESSION_HEARTBEAT` |

Produto `TURBORAMA_SUITE`, schema v1, DTOs, domínios de assinatura, RSA-PSS,
autoridade, TLS, CNG e identidade permanecem conforme o contrato original.
O cliente compartilhado precisa validar o `Kind` ES antes de assinar o desafio
ou enviar a prova. Servidor antigo ou cabeçalho removido não podem resultar em
abertura ou substituição da sessão Suite pelo ES.

## 4. Regras para painel, encerramento, capacidade e telemetria

**Painel e titularidade.** As sessões devem ser obtidas das tabelas Suite e ES
com escopo fixado pelo servidor. A presença atual por licença/dispositivo, sozinha,
não distingue aplicações. Os rótulos e timestamps exibidos precisam refletir a
sessão e seu último contato real. Identificador TS, posse de chave CNG, ledger
comercial, Windows, MAC ou IP não substituem uma conta humana autenticada. Sem
vínculo confiável entre conta autenticada e licença, o encerramento permanece
restrito ao administrador autorizado ou ao fluxo de recuperação já aprovado.

**Encerramento.** A confirmação se aplica a uma sessão ES selecionada e deve
revalidar ator, aplicação, alvo e versão da instância dentro da transação. Uma
confirmação antiga não pode atingir uma sessão aberta depois. Revogar somente o
alvo e seus desafios de renovação, sem incrementar a geração global da licença
ou afetar Suite, PIX e outros clientes. Preservar permissões, CSRF, step-up e
auditoria. Revogação impede a renovação; não promete fechar instantaneamente um
processo Windows.

**Capacidade.** Rever o isolamento dos limites por aplicação, pois as rotas
compartilhadas terão o mesmo caminho HTTP. Medir 500 e 1.000 sessões com NAT,
rajadas, reconexão e carga sustentada, registrando latências, erros, recursos,
pool PostgreSQL e locks. As estimativas de 200/400 requisições por segundo do
handoff não são capacidade medida. Falha, revogação ou excesso de carga de um
cliente não podem alterar a autorização ou a sessão de outro.

**MAC/IP.** O complemento preserva inventário, fingerprints, baseline de hardware
e vínculos existentes. IP vem da conexão e da cadeia de proxies confiáveis. MAC
depende de coleta explícita no cliente, com contrato adicional autenticado,
limites, proteção e retenção; não pode ser obtido diretamente de uma requisição
HTTP pela internet. Não alterar os DTOs canônicos v1 para inserir esses sinais.

**IP é exclusivamente informativo e diagnóstico.** Seu valor, mudança ou
divergência não podem causar bloqueio, encerramento, revogação, recusa de
reconexão ou nova ativação, nem diretamente nem por um score. Clientes no mesmo
NAT continuam independentes. Divergência isolada de MAC também não autoriza
essas medidas. Falha da telemetria complementar não cancela o licenciamento.

## 5. Sequência de continuidade

1. Implementar primeiro o despacho HTTP e o signer ES compartilhado na branch ES,
   com diff limitado e compatibilidade preservada. Para essa frente, a migration
   022 já oferece as tabelas previstas.
2. Validar o pipeline HTTP real, parsing do cabeçalho, quatro assinaturas,
   ausência/remoção do cabeçalho, servidor antigo, flag desabilitada e ES 1.0.1.
   Repetir as regressões Suite/PIX e os testes com PostgreSQL descartável,
   incluindo coexistência, replay, revogação e heartbeat concorrente.
3. Tratar painel, titularidade/encerramento, capacidade e MAC/IP como frentes
   separadas, com contratos e critérios próprios. Qualquer schema adicional
   deve ter justificativa e migration aditiva; não recriar cadastros.
4. Identificar o artefato validado por commit e SHA-256. Confirmar o processo,
   listener, configuração, migration 022 e proxy efetivos antes da implantação,
   preservando o release anterior e o rollback sem remoção de tabelas.
5. Homologar cliente e servidor juntos: mesmo TS e CNG, Suite e ES simultâneos,
   negativas previstas, isolamento entre clientes e compatibilidade anterior.
   Somente depois comunicar a disponibilidade do contrato ao frontend.

Os detalhes de implantação e os dez checkpoints de aceite permanecem no
[handoff recebido](HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md).
Não usar `ops/deploy-round12.sh` para instalar Suite: ele pertence ao PIX.

## 6. Entrega desta rodada

Este arquivo registra o recebimento, o entendimento e as evidências consultadas.
A alteração desta rodada é exclusivamente documental. Não houve build, execução
nova de testes de runtime, aplicação de migrations, deploy, reinício de serviço,
uso de licença real ou alteração de configuração, chave, licença ou sessão.

As funcionalidades futuras continuam pendentes de implementação, validação e
homologação. A publicação deste retorno não representa aceite operacional nem
comprovação de funcionamento em produção.
