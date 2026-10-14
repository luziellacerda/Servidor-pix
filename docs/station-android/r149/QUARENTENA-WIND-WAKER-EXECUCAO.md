# R149 — quarentena cirúrgica da imagem ilegível de Wind Waker

**Estado desta entrega: operador preparado e testado apenas em fixture; produção não alterada.**
O problema observado no A56 e a identidade dos bytes estão em
[`ERRO-LEITURA-WIND-WAKER-20261014.md`](ERRO-LEITURA-WIND-WAKER-20261014.md).
Esta operação afeta exclusivamente
`station_12b835c9539b4800a6bb29ca00a25f94`, imagem de 546.776.387 bytes,
SHA-256 `3106a01516f59854a55fd6343da8b0f489ca8cfc523f300bb1913d7f1c602a2f`.
Não toca a outra edição de Wind Waker nem outros jogos, motores, licenças,
redes ou APKs.

## Operador entregue

`docs/station-android/scripts/quarentenar-wind-waker-station.py` resolve o
índice **efetivamente usado** pelo PID de `turborama-station-api.service`, por
`Station__LibraryIndexFile` em `/proc`. Compara esse caminho com o diretório
privado do importador. Usa o `scan.lock` do importador, confere revisão e SHA
do índice, estado de origem e identidade completa da imagem antes de agir.
O modo padrão somente planeja. A aplicação exige um plano privado recém
gerado e recibo privado. Se a origem, o índice, o PID ou o plano mudar, a
aplicação recusa e precisa de novo planejamento.

Na aplicação, o operador:

1. Guarda cópia exata do índice e um manifesto de rollback em diretório
   privado 0700 criado em `/mnt/DADOS` pelo próprio operador.
2. Move **só o arquivo de origem identificado** para um diretório de
   quarentena no mesmo volume, fora de todas as pastas de plataformas
   varridas. Não apaga ou altera bytes e conserva o artefato imutável já
   publicado para auditoria e transferências previamente autorizadas.
3. Marca apenas o item afetado `catalogVisible=false`, remove seu descritor
   `artifact` e avança a revisão global em um índice publicado por rename
   atômico com `fsync`.
4. Registra SHA, revisão e caminho do backup em recibo privado. Não reinicia
   serviço, não faz deploy e não modifica a lógica do aplicativo.

O servidor atual recarrega uma revisão maior em até cerca de 10 segundos.
Autorizações emitidas antes da troca podem usar o snapshot anterior por até
120 segundos; a janela de autorização nova é de 60 segundos. Por isso, a
verificação operacional deve aguardar a recarga e depois 120 segundos antes
de declarar a quarentena efetiva para todos os grants anteriores.

## Execução no Linux autorizado

Antes de operar, confirmar que o script e o arquivo de configuração privado
correspondem ao importador e API atuais. O operador rejeita alterações de
estrutura e fontes inesperadas. Usar um diretório **novo** 0700 pertencente a
`lz-servidor`, por exemplo `/home/lz-servidor/station-r149-quarentena-privado`.
Os comandos seguintes são para um operador com autorização root local no
servidor; não funcionam apenas ao copiar o Git para outro computador.

```sh
python3 tests/TurboRamaSuiteOnlineServer.Tests/station_wind_waker_quarantine_tests.py
sudo python3 docs/station-android/scripts/quarentenar-wind-waker-station.py
sudo python3 docs/station-android/scripts/quarentenar-wind-waker-station.py \
  --plan-output /home/lz-servidor/station-r149-quarentena-privado/plano.json
```

Conferir o resumo impresso: item, revisão, SHA e tamanho esperados. O plano
integral fica privado (0600, root) e inclui caminhos da fonte e do índice.
Aplicar **somente** se o resumo corresponder ao baseline fresco:

```sh
sudo python3 docs/station-android/scripts/quarentenar-wind-waker-station.py \
  --apply /home/lz-servidor/station-r149-quarentena-privado/plano.json \
  --receipt /home/lz-servidor/station-r149-quarentena-privado/aplicado.json
```

O resumo de aplicação imprime `backupDirectory`; preservar esse diretório
root 0700. Se a execução for rejeitada, não contornar guardas: inspecionar o
estado novo, produzir plano novo e revisar. O operador só move a fonte quando
ela pertence exclusivamente ao item afetado no `state.json` do scanner e
quando a imagem contém exatamente o SHA, tamanho e cabeçalho diagnosticados.
Depois do rename do índice, uma falha de recibo ou leitura devolve um resumo
com `publicationState` e `recoveryNeeded=true`, código de saída 2 e o backup;
isso **não** significa que a quarentena deixou de ser publicada. Nessa
situção, usar `--recover` após conferir o estado, sem reaplicar o plano.

### Aceite verificável

- A revisão do catálogo autenticado em `GET /v1/station/catalog` avançou;
  o item `station_12b835c9539b4800a6bb29ca00a25f94` não aparece.
- Um `POST /v1/station/downloads/authorize` autenticado para esse ID recusa
  uma **nova** autorização com `STATION_ARTIFACT_NOT_READY`. A entrada de
  controle escolhida pelo operador ainda autoriza normalmente.
- A origem se encontra na quarentena privada com SHA e tamanho iguais; a
  origem não reapareceu na pasta GameCube após a varredura horária. O
  artefato publicado antigo permanece íntegro para auditoria.
- A API, seu PID, o Nginx, o scanner, as outras entradas e licenças continuam
  em serviço. Registrar esses resultados em um recibo de produção sem
  credenciais, caminhos privados ou bytes de ROM no Git.

Se uma imagem válida for localizada, conferir **o arquivo real** com
`dolphin-tool verify -i`, cabeçalho/região, SHA, tamanho e partida física.
Publicar como artefato imutável com identidade e revisão coerentes. A outra
edição de Wind Waker tem uma
[cópia local que passou verificação estática](VALIDACAO-WIND-WAKER-ALTERNATIVA-20261014.md),
mas o arquivo efetivamente servido no Linux e a jogabilidade prolongada ainda
precisam de confirmação. O usuário relatou que a segunda entrada baixada no
A56 entrou no jogo em 14/10/2026. Preservar saves e demais dados; não remover
automaticamente a instalação da outra entrada.

### Rollback protegido

Se for necessário reverter, usar o `backupDirectory` gerado, enquanto o
índice ainda corresponde exatamente ao resultado da quarentena:

```sh
sudo python3 docs/station-android/scripts/quarentenar-wind-waker-station.py \
  --rollback /mnt/DADOS/station-wind-waker-backup-20261014-EXATO \
  --receipt /home/lz-servidor/station-r149-quarentena-privado/revertido.json
```

O rollback verifica o índice, a imagem em quarentena, o PID e os hashes,
devolve somente a origem ao lugar anterior e publica o índice original com
revisão global nova e crescente. Se houve qualquer alteração posterior,
recusa para permitir conciliação consciente. Não restaurar manualmente um
índice antigo com revisão menor. O rollback reabre para download a imagem
comprovadamente defeituosa; usá-lo apenas para recuperar uma operação
malsucedida, nunca como conserto da mídia.

Se o processo cair depois de mover a origem, antes de terminar o recibo,
usar o **mesmo diretório de backup** com `--recover` e um recibo novo:

```sh
sudo python3 docs/station-android/scripts/quarentenar-wind-waker-station.py \
  --recover /mnt/DADOS/station-wind-waker-backup-20261014-EXATO \
  --receipt /home/lz-servidor/station-r149-quarentena-privado/recuperado.json
```

Se o índice antigo ainda está ativo e a fonte foi movida, a recuperação
devolve a fonte e deixa o índice intocado. Se o índice da quarentena já foi
publicado, confere que a fonte ficou em quarentena e conclui o recibo. Se o
índice do rollback já foi publicado, confere/restaura a fonte original e
conclui o recibo do rollback. Qualquer outro estado é recusado.

## Limites

Esta entrega **não comprova aplicação no servidor**: o host Linux não estava
acessível por SSH neste computador e a mídia `I:` não estava montada nesta
inspeção. Os dezesseis testes offline cobrem projeção de um único item, recusa de
hash divergente, isolamento da fonte, proteção contra fonte compartilhada,
rollback efetivo, guardas de caminho e falhas após publicação ou rollback, além de
recuperação da interrupção após a movimentação; o
recebimento e as provas HTTP precisam ser feitos no Linux.
