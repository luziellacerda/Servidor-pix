# APP → SERVIDOR: subpastas do catálogo Station

## Destinatário e finalidade

Este documento é uma entrega do aplicativo TurboStations Android para o operador do **Servidor-pix**. É uma solicitação nova de integração de subpastas, **não é um retorno do servidor nem uma ordem para repetir a implantação das salas online**.

O mantenedor pediu: entrar em uma plataforma, visualizar suas subpastas no carrossel nativo e abrir uma lista específica de jogos ao selecionar uma pasta. A implementação preparada organiza o **catálogo do servidor**. A pergunta sobre também ler pastas locais do telefone continua sem resposta. Não inferir importação de arquivos externos ou alteração dos diretórios de instalação.

## Revisões e produção que devem ser preservadas

- Retorno online recebido: commit `a4814d453a1f193fcbe40a92c8690c4eb5d41fc9`, branch `feat/station-online-direct-20261004` / `review/station-online-handoff-20261004`.
- O retorno relata salas publicadas às 11h29 de 04/10/2026, fonte da DLL `77d1dfb50a9982b01d8d649db477e6268dc7a5fb`, SHA256 `ff6362852635d4d18a01e85e46c89ad5cc2a7dad75d3b99793a124733beb6509`.
- Índice relatado: revisão 4, 1.816 jogos, SHA256 `c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a`. Estas são evidências publicadas pelo operador; este trabalho Windows não executou comandos no Linux.
- A alteração de pastas deve partir dessa revisão ou de uma sucessora conciliada. Não substituir o serviço pelo candidato online anterior. Preservar validação de páginas, rejeição de JSON duplicado, registro de motores, proxy e configurações efetivamente publicados.
- As pastas de produção e o índice efetivo devem ser confirmados pelo operador. O gerador desta entrega exige raízes explícitas; não contém montagens presumidas.
- Não executar novamente `--apply` ou `--resume` da publicação online encerrada. Fazer implantação própria da nova revisão apenas após a homologação e os critérios abaixo.

## Estado exato do aplicativo

APK candidato R9: `TurboStations-Salas-Subpastas-R9-20261004.apk`.

SHA256: `b5c98ea40b915f738e29ef2b2e7168fcdf2d327aa64c862596dff15b5620fdf1`.

Tamanho: 1.982.754.504 bytes. Pacote `org.turboramastation.frontend`. Certificado existente preservado. Fonte canônica: `E:\ESTUDO APK\work\station-netplay-20261004`. Entrega no TurboElden em `versions/station-ui-folders-r9-20261004`; o recibo de publicação identifica seu commit completo.

R9 foi compilado, assinado e conferido no PC. Ainda não instalado: USB ausente. Último instalado e observado é R8 `8e76d832…`. R8B só ficou no PC. Preservar a tag estável anterior; candidato não significa estabilidade ou partida homologada.

R9 também redesenha as salas. **O design não exige uma rota nova.** Os dois POSTs existentes continuam `/v1/station/online/command` e `/v1/station/online/events`, com a sessão atual, assinaturas e hashes de jogo/motor. A homologação P2P entre dois aparelhos continua pendente.

## Contrato aditivo exato

Rota mantida: `GET /v1/station/catalog`, com Bearer e o envelope assinado atual. Dentro de cada item, acrescentar somente:

```json
{
  "itemId": "station_fixture001",
  "name": "Jogo de exemplo",
  "platform": "snes",
  "revision": 1,
  "coverId": "cover_fixture001",
  "folderPath": ["Selecionados", "Traduções"]
}
```

Os IDs acima são fictícios. Não inserir esses itens em produção.

| Campo/regra | Comportamento |
| --- | --- |
| `folderPath` ausente | Jogo fica na raiz; compatibilidade com o catálogo anterior |
| `folderPath: []` | Também representa a raiz |
| Array não vazio | Segmentos relativos dentro da própria plataforma, sem repetir `snes` |
| Profundidade | Máximo de 8 segmentos |
| Nome de segmento | De 1 a 80 unidades UTF-16, Unicode válido |
| Inválidos | Nulo, tipo diferente de array/string, vazio, só espaços, controles, `/`, `\`, `.` e `..` |
| Identidade | Não alterar `itemId`, `coverId`, plataforma ou revisão individual por mudar a apresentação |
| Revisão do catálogo | Incrementar quando mudar a organização das pastas |
| Limite atual | Continua 4.096 itens nesta linha; não misturar a versão de capacidade 40 mil |

`folderPath` precisa ficar **dentro dos bytes assinados**. Não montar um segundo JSON sem assinatura, uma URL externa ou uma rota alternativa. O envelope, domínio `catalog/v1`, produto, aplicação, licença, aparelho e sessão continuam iguais.

O campo é somente apresentação: jamais é usado como destino de gravação, `launchPath`, endereço de download, autorização, hash ou caminho absoluto do servidor. Não enviar `filePath` ou `coverPath` privados ao telefone. Downloads continuam autorizados por `itemId` e descritor assinado; capas continuam por `coverId`.

## Alterações de código entregues

1. `StationLibrary.cs`: propriedade `FolderPath` com valor padrão vazio; parser estrito do novo campo no índice privado. Arquivos antigos continuam aceitos.
2. `StationService.CatalogAsync`: inclui `folderPath = item.FolderPath` na mesma projeção já enviada ao assinador.
3. `prepare_station_folder_index.py`: produz **outro arquivo de índice**, com raízes confirmadas por plataforma. Preserva todas as propriedades de cada item, exceto o novo metadado; preserva a ordem e incrementa somente a revisão global quando necessário.
4. Teste C# usa o parser real, não um parser simulado. Os testes locais passaram para raiz, níveis, Unicode, recusas e preservação de identidade.

O patch tem apenas essas alterações de apresentação. Sem migrations, tabelas, novas chaves, novas rotas, mudanças em grants, cache, downloads ou outros produtos.

## Gerar o índice sem adivinhação

O operador deve identificar o índice efetivo e a raiz **real** de ROMs de cada plataforma. Uma pasta virtual já escolhida manualmente pode ser informada diretamente em `folderPath` no índice; o script conserva metadados existentes por padrão.

Exemplo estrutural de execução, usando variáveis previamente preenchidas pelo operador:

```bash
python3 docs/station-android/subpastas-20261004/prepare_station_folder_index.py \
  --input "$INDICE_EFETIVO" \
  --output "$NOVO_INDICE_CANDIDATO" \
  --platform-root "snes=$RAIZ_SNES_CONFIRMADA" \
  --platform-root "megadrive=$RAIZ_MEGA_CONFIRMADA" \
  --receipt "$NOVO_RECIBO"
```

Isso **não publica** o índice. O script recusa saída já existente, sobrescrita da entrada, raiz relativa/inexistente, arquivo fora da raiz e plataforma informada sem correspondência. Resolve o caminho real antes de conferir que está dentro da raiz. Não concatena caminhos recebidos do app.

Para cada arquivo já indexado, deriva somente os diretórios entre a raiz informada e seu diretório pai. Não acrescenta jogos ao catálogo, não cria IDs e não transforma o nome do jogo em pasta. Se for preciso reorganizar metadados já presentes, `--replace-folder-path` é uma escolha explícita do operador.

Pastas vazias não aparecem, pois a hierarquia desta versão deriva dos itens. Não inventar jogos para exibir pastas vazias. Representação de pastas vazias precisaria de contrato adicional.

## Como o aplicativo usa a hierarquia

1. Verifica o envelope assinado e valida os segmentos; grava o metadado no cache local do catálogo.
2. Publica um sétimo campo opcional na ponte Java/JNI; os seis campos anteriores continuam reconhecidos.
3. Aplica o mapa de pastas junto com o catálogo na thread SDL. Não modifica o tamanho das estruturas nativas originais.
4. Ao selecionar uma plataforma com hierarquia, abre uma fachada de pastas no **carrossel nativo**.
5. A raiz oferece `Todos os jogos`, `Jogos sem subpasta` quando aplicável e as pastas imediatas. Um nível intermediário oferece seus filhos e `Jogos desta pasta` quando houver itens diretos.
6. Ao abrir uma folha, filtra os **índices originais dos jogos**. Baixar, jogar, instalado, apagar e pesquisa continuam na plataforma/pasta selecionada.
7. Voltar retorna um nível; o botão Plataformas retorna à seleção de sistemas.
8. Sem `folderPath`, permanece o fluxo anterior direto aos jogos. Não há uma árvore fictícia no APK.

Vídeos ficam pausados nas pastas. A imagem de uma pasta usa uma capa local de seus jogos ou a arte do console. Nenhum pedido de capa adicional foi criado para a hierarquia.

## Homologação exigida antes de publicar

- Aplicar o patch na revisão conciliada e compilar; conferir que as correções online recentes permanecem no diff.
- Gerar o índice candidato, comparar número de itens, ordem, IDs, plataformas, capas, descritores e caminhos com o índice anterior. Só `folderPath` e revisão global devem mudar.
- Usar catálogo sintético com raiz, dois níveis, nomes Unicode e duas plataformas. Rejeitar todos os casos inválidos da tabela.
- Conferir `GET /catalog` autenticado: assinatura e vínculos válidos, array de segmentos correto, nenhum caminho privado exposto.
- Testar o cliente antigo com o novo catálogo e R9 com catálogo sem metadado.
- Executar regressões de perfil, sessão, 4 capas simultâneas, autorização, transferência/hash e salas online. Não deduzir sucesso dessas rotas somente pela compilação de pastas.
- Confirmar leitura do novo índice e dos arquivos sob o usuário real do serviço antes da ativação.
- Preparar backup restaurável do índice e da release efetivos; retorno deve restaurar a revisão anterior sem remover jogos/licenças/saves.

## Resposta que o operador deve publicar

Responder com um arquivo **RETORNO-SERVIDOR-SUBPASTAS-STATION-20261004.md**, apontando:

1. Branch e commit completos aplicados; fonte e SHA256 da DLL realmente executada.
2. Serviço, ExecStart, usuário, drop-ins e índice efetivos, sem segredos.
3. Revisão/contagens antes e depois, plataformas e profundidade; quantidade de itens cujo `folderPath` foi preenchido.
4. Comparação que demonstra preservação de IDs, capas, revisões individuais, descritores e arquivos.
5. Um exemplo sanitizado do payload **real assinado**, correlação, resultado HTTPS e verificação de assinatura.
6. Resultados das regressões, backup/retorno e distinção entre código, homologação, produção e aparelho.
7. Qualquer pendência real. Não marcar Android ou partida P2P como testados pelo Linux.

O app está preparado para consumir o campo; a hierarquia aparecerá quando o servidor publicar os metadados. Esta entrega não executou implantação remota.

## Publicação exata do aplicativo

Commit **a325e6986e5432871a0b8f22168e4e8e16868eff**, branch `feat/station-capas-visuais-netplay-20261003`. [Fonte, testes e recibo R9](https://github.com/luziellacerda/TurboElden/tree/a325e6986e5432871a0b8f22168e4e8e16868eff/versions/station-ui-folders-r9-20261004).
