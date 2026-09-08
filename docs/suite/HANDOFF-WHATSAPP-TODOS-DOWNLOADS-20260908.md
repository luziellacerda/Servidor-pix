# Handoff — WhatsApp para todos os downloads concluídos

Data: 08/09/2026, UTC−3.
Destino: responsáveis pelo servidor Linux e pelo programa Windows TurboRama Suite.
Estado deste documento: requisito atualizado e diagnóstico entregues; a ampliação
para todos os downloads ainda precisa ser implementada e testada.

## Pedido vigente do proprietário

**Todo download que concluir com sucesso deve gerar uma notificação automática
para o proprietário correto da compra, em todas as categorias e formatos,
tenha ocorrido descompactação ou não.**

O usuário pediu expressamente este handoff após informar que um jogo de Xbox foi
baixado sem mensagem. Este requisito substitui a limitação anterior a
`Extracting → Completed` e a regra antiga de que arquivo bruto não gera aviso.
O fechamento anterior fica como histórico da implantação e das evidências.

**A autorização geral dos disparos já foi concedida na conversa. Não pedir nova
autorização por arquivo, categoria, download ou destinatário legitimamente
resolvido pelo servidor.** Continuam obrigatórias compra paga, usuário ativo,
proprietário único, telefone válido e política de bloqueio. A autorização não
permite inventar destinatário, ignorar bloqueios ou enviar aviso de operação
incompleta.

## Diagnóstico que motivou a mudança

Na mesma conta do teste 3DS, foi encontrado **Halo 3 (Brazil)** em formato `.iso`,
com acesso autorizado às **16:02:25 UTC−3**. O catálogo apresenta
`visual_extract_policy=NONE` e `extract_policy=NONE`. Até a conferência das
16:12:21, havia **zero eventos de Xbox/Halo 3 e zero jobs correspondentes**.

O protocolo já aceita as categorias Xbox. O contexto de notificação desse item
está disponível e API/Admin/túnel estão ativos. O fluxo documentado do cliente,
porém, só registra a conclusão após extração. Um ISO mantido como arquivo não
passa por esse gatilho. O servidor confirmou a ausência de evento; o estado
interno do Windows ainda precisa ser conferido pelo responsável pelo programa.

Há também uma condição operacional separada: o timer de extração permanece
`disabled/inactive` enquanto o aceite está pendente. **Ligar esse timer, sozinho,
não cria o evento que o cliente deixou de emitir.**

O gateway opera em modo DIRECT: `AuthorizeDirectAsync` finaliza o grant antes de
retornar o redirecionamento HTTP 307. Portanto, `grant=COMPLETED` e seu horário
comprovam essa autorização, sem comprovar que todos os bytes chegaram ao PC.
Nunca disparar mensagens só porque o grant ficou `COMPLETED`, a URL foi liberada
ou uma requisição HTTP terminou.

## Comportamento obrigatório

Usar o estado final real da operação, com o arquivo/destino publicado e as
verificações aplicáveis concluídas. A extensão do arquivo, isoladamente, não
pode decidir se há notificação.

| Situação concluída no programa | Momento do aviso | Mensagem esperada |
|---|---|---|
| ISO de Xbox/PlayStation ou outra plataforma | Download finalizado e arquivo disponível no destino | Download concluído |
| ROM ou pacote mantido como `.cso`, `.3ds`, `.nsp`, `.xci`, `.chd` etc. | Download finalizado e arquivo disponível no destino | Download concluído |
| ZIP/RAR/7z mantido sem extração | Download finalizado e arquivo disponível no destino | Download concluído |
| Conteúdo com extração configurada | Download, extração e publicação final concluídos | Download e descompactação concluídos |
| Emulador, utilitário ou qualquer outra categoria suportada | Estado final de sucesso correspondente à operação | Aviso correspondente ao resultado real |
| Pausado, cancelado, incompleto, com falha ou verificação reprovada | Não é conclusão bem-sucedida | Não enviar aviso de sucesso |

Quando há extração na operação, enviar uma única mensagem final após essa etapa;
não mandar outra mensagem intermediária só porque o download dos bytes acabou.
Clicar em abrir pasta/arquivo, abrir novamente o programa, limpar cache ou repetir
a entrega HTTP do mesmo evento não deve criar novo disparo.

Interpretação operacional de “todos os downloads”: cada operação distinta que
realmente baixar e concluir novamente tem seu próprio aviso. Reabertura,
retomada e retry da mesma operação continuam sendo o mesmo evento. Usar uma
identidade persistente da operação, sem gerar um novo identificador em cada retry.

## Trabalho necessário no programa Windows

1. Localizar todos os caminhos que publicam uma conclusão com sucesso, incluindo
   o caminho sem extração. Centralizar a emissão do aviso nessa conclusão.
2. Manter o disparo de arquivos extraídos e incluir arquivos finais sem extração.
   Verificar o destino final antes de registrar o evento; não usar apenas 100% na
   barra de progresso ou o recebimento do link de download.
3. Registrar tipo de conclusão, identidade da operação/conteúdo e horário real.
   Persistir o aviso protegido fora de `.turborama-downloads`, antes de depender
   da rede. Reaproveitar identidade, sessão e assinatura já existentes.
4. Enviar a afirmação assinada de conclusão à API e remover o aviso local somente
   após ACK válido, vinculado ao evento, `ACCEPTED` ou `ALREADY_ACCEPTED`.
5. Preservar o evento nas falhas transitórias, saída do programa e troca de sessão;
   reenviar com a mesma identidade e prova atualizada quando necessário.
6. Não enviar telefone, nome do cliente, chave do provedor, URL privada ou caminho
   local como dados de roteamento. O servidor resolve o destinatário.
7. Na atualização, preservar avisos antigos protegidos que ainda aguardam ACK.
   Não emitir simultaneamente o contrato antigo e o novo para a mesma conclusão,
   nem reinterpretar todo histórico local como novos downloads.

O servidor não consegue concluir sozinho esse trabalho: é o Windows que observa
a gravação final e a eventual extração. Entregar uma alteração coordenada das
duas partes, com a versão exata do EXE e do servidor documentada.

## Trabalho necessário no servidor Linux

Reaproveitar a infraestrutura de assinatura, validação de compra/proprietário,
outbox, worker, fila TurboBox e processador já existentes. Evitar uma segunda
cadeia de envio para a mesma conclusão.

- Generalizar o contrato para conclusão de download, distinguindo resultado com
  e sem extração. Versionar os campos e a assinatura de forma compatível.
  O JSON atual é estrito (`UnmappedMemberHandling.Disallow`); acrescentar campos
  ao cliente sem adaptar a API fará a requisição falhar.
- Preservar a rota antiga `/v1/suite/notifications/extraction-completed` para os
  avisos já existentes e clientes em transição. Se uma rota/versão genérica for
  adicionada, documentar o contrato e como ambas usam a mesma proteção contra
  duplicação, sem enviar duas mensagens da mesma operação.
- Vincular cada evento à identidade ativa, compra paga e conteúdo autorizado.
  Em DIRECT, o hash calculado pelo cliente é uma afirmação assinada do cliente,
  sem tratá-lo como hash autoritativo que o servidor verificou no disco do PC.
- Resolver o destinatário pelo relacionamento existente:
  `source_purchase_id → payment_orders(paid) → purchases(paid) → users(active)`.
  Destinatário ausente, ambíguo ou bloqueado deve conservar o tratamento previsto
  e produzir diagnóstico; nunca escolher outro usuário/telefone manualmente.
- Rever a deduplicação de `ExtractionCompletionProtocol.EventId`: hoje a chave
  usa licença/dispositivo/conteúdo/versão/manifesto/hash e não inclui uma operação
  distinta de download. Isso elimina também novos downloads do mesmo artefato.
  A nova semântica deve distinguir uma nova conclusão real de um retry, preservando
  IDs e estados dos eventos antigos durante a transição.
- Preservar lease, limite de tentativas, quota, fronteira `DISPATCHING` e tratamento
  de `UNCERTAIN`. Não reenviar cegamente após resultado ambíguo.
- Usar migration aditiva se necessária. A 026 está aplicada; verificar o próximo
  número livre antes de criar outra. Não editar/apagar a migration já aplicada,
  filas, histórico de envios, compras, licenças ou identidades para obter sucesso.
- Reaproveitar o agendamento existente, documentando eventual atualização de
  nomes/configuração. Não deixar dois timers consumindo o mesmo fluxo por engano.

## Texto e comprovação do envio

O texto atual sempre afirma “DOWNLOAD E DESCOMPACTAÇÃO CONCLUÍDOS” e parte das
10 variações também fala de extração. Ajustar título e todas as variações para
corresponder ao tipo real de conclusão. Um ISO sem extração deve informar
**DOWNLOAD CONCLUÍDO**. Não afirmar descompactação ou verificação de integridade
que não ocorreu. Manter conteúdo, categoria, horário e protocolo; resolver nome
no servidor e preservar a ausência de dados técnicos privados na mensagem.

O teste já executado do 3DS gerou um único job **73**, `sent`, uma tentativa,
enviado às **15:54:58 UTC−3**, com `api_message_id=NULL`. A biblioteca captura
somente `messageId`/`id` no primeiro nível e não persiste a resposta original.
A [documentação MenuIA de envio de texto](https://docs.menuia.com/api-reference/create-message/mensagemTexto)
apresenta sucesso com `status` e `message`, sem ID. O ID ausente sozinho não
comprova falha nem autoriza repetir esse envio.

Registrar evidência sanitizada da aceitação pelo provedor, horário, resultado e
ID quando fornecido. Se não for fornecido, registrar essa limitação explicitamente,
sem fabricar ID. Correlacionar evento, job, protocolo e recebimento real no aparelho.
`ACCEPTED`, `QUEUED` e `sent` são etapas diferentes; não declarar recebimento no
WhatsApp apenas pelo status local. Se for necessário melhorar o registro do
processador compartilhado, testar também o aviso de conexão antes de implantar.

## Estado de produção a preservar

Base documental antes deste handoff: commit `2120bab7b5dbe1dab17dd31b7a27fff5ed878ea8`,
branch `codex/fechamento-disparos-whatsapp-20260908`, repositório
`luziellacerda/Servidor-pix`. Revalidar HEAD e runtime ao retomar.

- Runtime API/Admin: commit `353ab1d729ad625a986c96f85f3afa4a306cc1dd`, release
  `/opt/turborama-suite-r5-releases/extraction-353ab1d-20260908`; migration 026 ativa.
- Rota de extração publicada, flags habilitadas; corpo `{}` responde 400.
- 3DS `29c1f7bc6e83`: `QUEUED`, attempts=1; job 73 `sent`. Não reenviar esse evento.
- PS Vita `6dbeb98929e7`: `PENDING`, attempts=0; zero jobs na última conciliação.
  Pertence à mesma conta do 3DS, portanto não comprova isolamento com Conta B.
- Halo 3: nenhum evento recebido; não criar aviso retroativo a partir do grant.
- `turborama-suite-extraction-whatsapp.timer`: disabled/inactive.
- `turborama-suite-connection-whatsapp.timer`: enabled/active.
- Processador real: **PM2 `turbobox-notifications`**, executando
  `process-notifications.php`, online. A unit systemd de mesmo nome citada no
  primeiro handoff não existe; não instalar um substituto por esse motivo.
- API/Admin/PIX/gateway/Cloudflared/Nginx ativos. Preservar DNS, portas, túnel,
  certificados, bancos existentes, cadastro e funcionamento do login.

As 15 sondagens HTTP da implantação anterior passaram; as verificações do
servidor para Xbox foram somente leitura. Isso **não significa que a ampliação
para todos os downloads já esteja implementada ou aprovada**.

## Testes e liberação obrigatórios

1. Reproduzir um download real de ISO, preferencialmente Halo 3: conclusão no
   Windows → evento persistido → ACK → uma outbox → um job → provedor → aparelho.
2. Testar arquivo final de outra categoria, como `.cso`, `.3ds` ou `.nsp`, e um
   ZIP/RAR mantido sem extração. Todos devem produzir aviso de download concluído.
3. Testar uma operação com extração: uma única mensagem final, com texto correto.
4. Reabrir o programa, reconectar e repetir o mesmo POST: nenhum segundo job;
   ACK idempotente. Retomar uma transferência não cria outra operação.
5. Fazer um novo download real do mesmo conteúdo: uma nova conclusão deve gerar
   seu próprio aviso, sem confundir esse caso com retry ou reabertura.
6. Testar falha, cancelamento, arquivo parcial e verificação reprovada: zero avisos
   falsos de sucesso. Falha opcional da notificação não desfaz o download concluído.
7. Validar duas contas pagas distintas, cada mensagem no proprietário correto;
   autorização operacional geral já concedida. Confirmar recebimento real.
8. Testar cliente antigo/novo e avisos pendentes durante a atualização. Não perder
   aviso, converter `QUEUED` em novo envio nem duplicar por mudança de protocolo.
9. Em homologação, testar concorrência, ACK perdido e resultado ambíguo; nunca
   provocar timeout deliberado no provedor real para esse teste.
10. Conferir regressões de login, heartbeat, download, catálogo, PIX,
    EmulationStation e aviso de conexão, com testes compatíveis com a mudança.
11. Com os critérios funcionais satisfeitos, habilitar o timer existente. Confirmar
    `enabled/active`, próximas execuções e processamento automático de novas
    solicitações válidas sem intervenção ou autorização manual por arquivo.
12. Observar quatro ciclos, 15 minutos e 1 hora: sem duplicações, filas presas,
    exposição de dados ou regressões. Registrar qualquer pendência com precisão.

## Entrega e retorno

Implementar e testar as mudanças coordenadas; devolver o resultado neste mesmo
handoff, com commit/branch, versão e hash do EXE, release/hash API/Admin,
migrations realmente aplicadas, contrato publicado e compatibilidade.

Para cada caso real, registrar somente conteúdo, categoria, horário, prefixo do
evento, proprietário mascarado, últimos quatro dígitos, job/status, resultado do
provedor e confirmação do aparelho. Manter logs sensíveis e credenciais fora do Git.

Informar estado final do timer, quatro ciclos/15 minutos/1 hora, regressões e
limitações. Resultado: `APROVADO`, `BLOQUEADO` ou `REVERTIDO`, com motivo concreto.
Não apresentar este documento como prova de execução da correção.

Diagnóstico detalhado: [retorno anterior e análise Xbox](HANDOFF-FECHAMENTO-DISPAROS-WHATSAPP-20260908.md).
Evidência privada Xbox:
`/home/lz-servidor/evidence/extraction-whatsapp-20260908/xbox-analysis-20260908T190954Z/`.
Evidência privada do disparo 3DS:
`/home/lz-servidor/evidence/extraction-whatsapp-20260908/closeout-20260908T183400Z/`.
