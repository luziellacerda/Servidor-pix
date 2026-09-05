# EmulationStation com a ativacao do TurboRama Suite — servidor 1.0.0

Esta extensao permite que o EmulationStation sem servicos comerciais consulte a
mesma licenca ja ativada no TurboRama Suite. O computador prova que possui a chave
CNG que foi cadastrada na ativacao. O servidor continua sendo a autoridade sobre
licenca, equipamento, suspensao, revogacao e transferencia.

Base deste trabalho: `codex/turborama-suite-vendas-producao-20260828`, commit
`4ea972657355740485b3831970ef1fd21b186661`. Os arquivos do servidor Suite e suas
migrations nessa revisao sao identicos aos da branch
`codex/suite-whatsapp-session-open-20260903`, commit
`fb3d304645b1c1657b23c45a599abfe5a1fdc2e7`.

Branch da extensao: `codex/emulationstation-suite-v1-20260905`.

## Por que existe um caminho proprio

O servidor original guarda uma sessao por `(license_id, device_id)` na tabela
`suite.suite_sessions`. Uma nova abertura substitui o identificador da anterior.
Se o EmulationStation usasse esse mesmo caminho, fecharia a autorizacao da loja
Suite no proximo heartbeat. A nova extensao usa tabelas separadas para os seus
desafios e sessoes. As duas aplicacoes podem executar ao mesmo tempo, com a mesma
licenca e a mesma identidade, sem substituir suas sessoes mutuamente.

## Contrato que o EXE deve usar

| Operacao | Caminho HTTP |
| --- | --- |
| Obter desafio de abertura ou heartbeat | `POST /v1/suite/emulationstation/challenges` |
| Enviar prova e obter sessao assinada | `POST /v1/suite/emulationstation/sessions` |

As estruturas JSON continuam sendo `ChallengeRequest`, `SessionProof`,
`SessionContext` e `SignedAssertionEnvelope` do protocolo Suite v1. O produto
continua `TURBORAMA_SUITE`. Somente `session.open` e `session.heartbeat` sao aceitos.
Os nomes dos envelopes assinados, a serializacao canonica, RSA-PSS-SHA256, os
dominios de assinatura e a autoridade publica assinada permanecem os existentes.
O cliente deve mudar os dois caminhos de sessao, validar a mesma autoridade/TLS e
abrir a chave CNG existente do usuario Windows. Criar outra chave nao aproveita a
ativacao anterior e nao deve ser um fallback do EmulationStation.

O servidor nao possui uma rota publica que descubra o `LicenseId` pelo `DeviceId`.
O identificador da licenca deve ser informado ao cliente ou lembrado localmente
depois de uma validacao. Ele nao e o codigo de ativacao de uso unico. Esta extensao
nao adiciona ativacao, exportacao de chave privada, codigo universal nem licenca
offline. Dados reais de teste nunca devem ser gravados no repositorio, workflow,
argumentos de compilacao ou artefatos.

## Codigo, do inicio da requisicao ate a autorizacao

1. `Program.cs` le `Suite:EmulationStation:Enabled`. O valor padrao e `false`, e a
   extensao tambem exige que o servico Suite esteja habilitado. A configuracao
   publicada continua desabilitada.
2. `EmulationStationEndpoints` registra somente as duas rotas. Desabilitada, a
   extensao responde HTTP 503 / `EMULATIONSTATION_DISABLED` antes de acessar banco
   ou chaves. Habilitada, aplica JSON estrito, limite de corpo do Kestrel,
   rate limiter existente e timeout de dez segundos. Erros nao incluem segredos.
3. `EmulationStationService` recusa qualquer acao de ativacao, inventario,
   catalogo ou download. Delega a validacao criptografica ao `SuiteService`
   existente usando exclusivamente `IEmulationStationStore`.
4. `PostgresEmulationStationStore` le licencas e equipamentos das tabelas Suite
   existentes. Um desafio exige uma licenca vinculada, ativacao consumida e
   equipamento ativo. Ele grava apenas `suite.suite_es_challenges`, incluindo a
   geracao atual de revogacao. Desafios vencidos da mesma licenca sao removidos
   durante a proxima emissao; ha um limite de 64 desafios por equipamento ainda
   dentro do prazo. A assinatura do desafio usa a autoridade online existente.
5. O EXE assina o desafio usando a chave privada CNG existente. O `SuiteService`
   confere contexto, produto, licenca, equipamento, sessao, fingerprint, hash,
   desafio, nonce e assinatura. O adapter procura o desafio somente na tabela ES.
6. A conclusao ocorre em transacao serializavel. Ela bloqueia as linhas da licenca,
   entrega comercial quando houver, vinculo e equipamento. Confere estado ativo,
   licenca vitalicia para um equipamento, ativacao consumida e entrega elegivel.
   A prova consome uma unica vez o desafio ES, com todos os campos e a geracao
   de revogacao correspondentes, antes da alteracao da sessao ES na mesma transacao.
7. `session.open` substitui somente a sessao ES daquele equipamento.
   `session.heartbeat` renova somente a mesma sessao ES ainda ativa e nao vencida.
   Repeticao, geracao antiga, sessao substituida ou expirada sao negadas. Falhas de
   serializacao/deadlock recebem no maximo tres tentativas controladas.
8. A resposta assinada preserva o prazo de 180 segundos e heartbeat sugerido de
   cinco segundos do Suite. O EXE precisa verificar a assinatura e o prazo com
   relogio monotonicamente limitado. Uma resposta local `ACTIVE` sem verificacao
   criptografica nao e autorizacao. A falta de servidor na abertura deve negar
   entrada; falha temporaria durante uso nao pode estender o prazo localmente.

Uma prova ES enviada para `/v1/suite/sessions` falha porque o identificador nao
existe na tabela de desafios Suite. O inverso tambem falha. O acesso a catalogo
exige a sessao na tabela original; a sessao ES nao concede esse acesso. A
isolacao e feita pelas rotas, allowlist de acoes, tabelas e consumo transacional;
os envelopes assinados nao foram transformados em tokens transferiveis de acesso.

## Migracao e habilitacao operacional

O workflow gera um artefato para revisao. Ele **nao implanta, habilita, modifica
variaveis de producao, emite licencas ou altera chaves**. O deploy e uma etapa
operacional separada. Ate a extensao ser implantada e habilitada no servidor
existente, o novo EXE deve recusar login com uma mensagem de indisponibilidade.

No ambiente de homologacao, com backup e acesso administrativo ao banco correto:

1. Confirme que as migrations Suite 001–021 ja foram aplicadas. Compare a revisao
   efetivamente implantada; este documento nao prova qual commit esta em producao.
2. Aplique apenas
   `migrations/suite/022_suite_emulationstation_sessions.up.sql`, uma vez, usando
   `psql --set=ON_ERROR_STOP=1 --file=...`. A migration adiciona duas tabelas e seus
   indices, concede permissoes somente ao papel runtime `turborama-suite` e registra
   a versao. Ela nao altera as tabelas de sessao/desafio Suite ou do PIX.
3. Publique o servidor candidato conforme o procedimento operacional existente.
   Mantenha as mesmas configuracoes protegidas, chave de assinatura e TLS. O novo
   recurso nao requer uma nova chave nem permissao de administrador no EXE.
4. No servico Suite de homologacao, configure
   `Suite__EmulationStation__Enabled=true`, preservando `Suite__Enabled` e as
   demais configuracoes. Recarregue apenas o servico Suite pelo procedimento
   operacional estabelecido.
5. Se o proxy publicar rotas individualmente, inclua os dois caminhos ES no mesmo
   upstream Suite ja aprovado. Nao encaminhe esses caminhos ao processo PIX.
6. Teste login com a licenca ativada e a mesma conta Windows, Suite e ES abertos
   juntos, suspensao/revogacao, falta de rede, copia para outro PC e outro usuario
   Windows. Confirme que os eventos e downloads da Suite continuam funcionando.

Rollback operacional: configure `Suite__EmulationStation__Enabled=false` e
recarregue somente o servico Suite. O EXE ES perde a autorizacao conforme o
heartbeat/prazo valido. A Suite e o PIX seguem os fluxos anteriores. As tabelas ES
podem permanecer para auditoria; nao ha necessidade de apaga-las para retornar ao
binario anterior. Uma remocao definitiva dessas tabelas e uma acao administrativa
separada, com exportacao previa se necessario.

O papel Suite usa as mesmas permissoes existentes para ler/bloquear licencas,
vinculos, equipamentos e entregas. A migration 022 concede apenas as novas
permissoes de escrita nas tabelas ES. A extensao nao publica inventario, presenca
ou avisos WhatsApp e nao altera a logica dos servicos existentes.

## Verificacao e limites

`tests/TurboRamaSuiteEmulationStation.Tests` usa chaves RSA e identificadores
estritamente sinteticos. Os testes verificam assinatura de resposta, uso do
produto Suite, coexistencia de sessoes, troca de sessao somente no ES, rejeicao
de prova entre caminhos, replay, acao proibida, catalogo, hardware/chave incorretos
e rotas desabilitadas. Com `SUITE_ES_TEST_CONNECTION` e `--require-postgres`,
executam o `PostgresSuiteStore` original e o novo adapter no PostgreSQL real,
incluindo revogacao, geracao antiga apos suspender/retomar, dispositivo revogado,
heartbeat expirado e ativacao obrigatoria.

Comandos de verificacao:

```text
dotnet restore tests/TurboRamaSuiteEmulationStation.Tests --locked-mode
dotnet run --project tests/TurboRamaSuiteEmulationStation.Tests -c Release
dotnet run --project tests/TurboRamaSuiteEmulationStation.Tests -c Release -- --require-postgres
```

O ultimo comando exige uma conexao para banco de testes isolado, com migrations
aplicadas. Nunca use a conexao de producao. O workflow
`.github/workflows/emulationstation-suite.yml` cria PostgreSQL 16 descartavel,
aplica migrations 001–022, executa os testes reais e os testes de protocolo Suite
anteriores, e publica um artefato separado com o recurso desabilitado. Ele nao
depende de compilador instalado no PC do cliente.
