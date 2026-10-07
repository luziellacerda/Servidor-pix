# APP → SERVIDOR: Neo Geo cartucho, CD e coleções KOF — R65

07/10/2026. **Este é um pedido técnico do cliente Android para o operador do Servidor-pix. Não é resposta do servidor, não é prova de produção corrigida e não autoriza alterar outros produtos.**

O mantenedor relatou que Neo Geo e Neo Geo CD, especialmente a coleção The King of Fighters, não abrem corretamente, e pediu analisar tudo que usa cartucho/CD. O pedido de recuperação online [Q01–Q08](PEDIDO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md) permanece aberto e separado. Nenhum timeout, protocolo ou motor online mudou nesta entrega.

## 1. Cliente exato que o servidor deve considerar

- Repositório: `luziellacerda/TurboElden`.
- Branch: `fix/station-neogeo-cd-r65-20261007`.
- Commit completo: **`e99f6fa705f4629085c426ffb7cf02b4fc624239`**.
- [Relatório, fontes e reprodução do app](https://github.com/luziellacerda/TurboElden/blob/e99f6fa705f4629085c426ffb7cf02b4fc624239/versions/station-neogeo-cd-r65-20261007/README.md).
- [Auditoria completa dos 189 nomes de cartucho](https://github.com/luziellacerda/TurboElden/blob/e99f6fa705f4629085c426ffb7cf02b4fc624239/versions/station-neogeo-cd-r65-20261007/evidence/driver-catalog-audit.json).
- [Recibo de compilação/empacotamento](https://github.com/luziellacerda/TurboElden/blob/e99f6fa705f4629085c426ffb7cf02b4fc624239/versions/station-neogeo-cd-r65-20261007/BUILD-RECEIPT.json).
- APK candidato R65: SHA-256 **`1858459b62a38a85cdd9fbeaafc68d3560008f154f99dd1431752aa0594eb081`**, 2.093.415.593 bytes.
- Base R64 SHA `e2bb778319e03d878ac5de3749f06af1b5e7bbffd06b8232402d2d2f610ba868`.
- Só `classes30.dex` alterado: SHA **`9dc641418b05663f0b548dbb01b3e2dab398fd73122a2b734b8509c19f68b080`**; 13.218 demais entradas conferidas/preservadas.
- MAME4droid Current 1.41.2 / MAME 0.289. Bibliotecas nativas idênticas às do doador oficial comparado; hashes no relatório.
- 37 testes de helper/CLI passaram e a recompilação independente produziu DEX idêntico. Não são testes físicos de gameplay.
- **R65 não instalada. Última instalação conferida é R63**, SHA `d9a35602ddc1141617df70d4b2b1f202d8646c538d4508f5fb3e53ef6b7abc4c`. A USB desconectou durante a análise de KOF '98. Não houve limpeza de dados/configurações.

## 2. Defeito do cliente CD encontrado e tratado

O helper `NeoCdSupport` de `versions/station-neogeocd-20261005` existia nos fontes, mas **não estava no APK R63/R64**. O antigo `classes30.dex` (`0d76beace3c7c427c670dc4cc59d60200d14b11f96b907fe79c8abc3e25d973f`) abria o CHD como entrada genérica, sem `neocdz`/`-cdrom`. Essa lacuna do APP não é atribuída ao servidor.

Na R65, a plataforma é determinada pelo ancestral `neo-geo-cd`/`neogeocd`; cabeçalho de CHD v5 autônomo e dependências são conferidos fora da thread de interface. Com firmware CD válido e auxiliar `000-lo.lo`, prepara `neocdz.zip` e abre:

```text
ACTION_VIEW: <content real>/neocdz.zip
cli_params: -rompath '<content real>' -cdrom '<disco.chd absoluto>' -bios official|unibios33|unibios32
```

Firmware ausente gera importação explícita, por seletor Android, persistida em `files/mame/station-bios/neogeocd`. A BIOS de cartucho pode fornecer o auxiliar, mas não substitui firmware CD. Nenhuma BIOS/ROM foi adicionada ao APK/Git. Os 50 arquivos iniciais CD do TSV analisado terminam em `.chd`; outras formas de mídia não foram implementadas por inferência.

Não restaurar Activities ou receitas antigas por causa desta integração. R65 preserva a R64 completa, Binder/saída, licença, controles e configurações nativas, carrossel, faixa INSTALADO e protocolo atual de salas. Aguardando validação Android com BIOS e mídia reais.

## 3. Evidência do catálogo, delimitada

Base analisada: **este repositório**, commit `8d9c670ba813fb970a61ff9bf329e6286b51ad07`, arquivo `docs/station-android/biblioteca-neogeocd-20261005/catalogo-completo.tsv`, revisão documental **14**.

SHA-256 do TSV: `3dae0cfa3877fba31b9a347b58795fe0df95333aea628b9ea8be597b7ccfed6e`.

O documento contém 189 cartuchos e 50 CDs. Cruzamos os nomes de `artifactLaunchPath` com o **registro integral** `mame.lst` da tag oficial `mame0289`: 176 nomes registrados e 13 ausentes. Não consultamos nesta auditoria o catálogo HTTP vivo, o índice ativo ou os membros das ROMs no Linux. Se produção já mudou, fornecer a revisão ativa e a primeira divergência comprovada; não tratar o TSV histórico como produção atual.

### Nomes de conjunto que precisam de conferência

| Entrada no documento | Local |
|---|---|
| `kof96ae20.zip` | Coleção `# 3 - THE KING OF FIGTHERS COLEÇÃO #` |
| `kof97xt.zip` | Mesma coleção |
| `kof98ae.zip` | Mesma coleção |
| `kf2k4pls.zip` | Mesma coleção |
| `kf2k2plb.zip` | Mesma coleção |
| `kf2k2plc.zip` | Mesma coleção |
| `kogplus.zip` | Raiz Neo Geo |
| `ltorb1.zip` | Raiz Neo Geo |
| `neonopon.zip` | Raiz Neo Geo |
| `neopong.zip` | Raiz Neo Geo |
| `zintrkcd.zip` | Raiz Neo Geo |
| `mosyougi.zip` | Raiz Neo Geo |
| `tws96.zip` | Raiz Neo Geo |

A coleção KOF tem 26 itens: seis nomes ausentes e 20 registrados. Nome registrado não prova que ZIP, chips, BIOS e dependências são corretos. Nome ausente também não autoriza substituir ou renomear o arquivo cegamente. Para cada alias/hack, verificar os membros, tamanho, CRC/SHA e driver compatível antes de propor solução.

Dois nomes registrados no MAME estão classificados no documento como Neo Geo, mas pertencem a outro hardware: `pspikes.zip` (`vsystem/pspikes.cpp`) e `spy.zip` (`konami/spy.cpp`). Não mover IDs/plataformas/instalações sem plano de preservação e evidência do estado ativo.

Fontes oficiais: [registro](https://github.com/mamedev/mame/blob/mame0289/src/mame/mame.lst), [Neo Geo](https://github.com/mamedev/mame/blob/mame0289/src/mame/snk/neogeo.cpp), [Neo Geo CD](https://github.com/mamedev/mame/blob/mame0289/src/mame/snk/neogeocd.cpp), [regras de conjuntos](https://docs.mamedev.org/usingmame/aboutromsets.html). Hashes exatos estão no snapshot do app.

## 4. KOF '98 padrão: falha ainda aberta

`kof98.zip` é registrado no MAME e diferente de `kof98ae.zip`. A captura curta do aparelho R63 confirmou que `kof98.zip` chegou ao motor, com `-rompath` apontando ao pai correto na geração `content`. Houve uma imagem verde/branca corrompida, cerca de cinco segundos após a abertura, com os controles próprios do MAME. Não apareceu erro fatal/ROM ausente nessa janela.

Não concluímos que o defeito é da ROM, BIOS, GPU, conjunto pai, coleção ou servidor. Faltou acompanhar após a inicialização e inspecionar membros/CRC do conteúdo instalado: USB desconectada. O lado APP continuará essa correlação quando o telefone voltar. O lado SERVIDOR pode adiantar a auditoria do pacote realmente entregue para esse item sem alterar dados por tentativa.

O caminho da instalação é `roms/.station-v2/<plataforma>/<itemId>/install-*/content/<launchPath>`. O app usa a pasta real, não a raiz genérica Neo Geo. Um conjunto split não pode depender de um ZIP pai que esteja inacessível noutra geração. Conferir o pacote externo de transporte **e** o ZIP de ROM interno; não confundir extração do transporte com descompactação necessária do conjunto para MAME.

## 5. Resposta técnica solicitada — NG-01 a NG-07

1. **NG-01 — estado ativo:** branch/commit do serviço, revisão do catálogo/índice realmente atendido e contagens Neo Geo/cartucho/CD/KOF. Distinguir documento, teste isolado e produção. Relacionar o TSV14 à revisão ativa por IDs preservados.
2. **NG-02 — 13 nomes:** para cada entrada, informar itemId, revisão, launchPath ativo, inventário de membros com tamanho/CRC/SHA e conclusão de compatibilidade com MAME0.289. Propor alias somente se comprovado; variantes que precisam de outro driver/core devem ficar explicitamente separadas, sem trocar o motor do app por inferência.
3. **NG-03 — KOF98 padrão:** correlacionar o item que entrega `kof98.zip` ao artefato, seus hashes, membros, BIOS e dependências de conjunto pai. Registrar se é merged/split/non-merged e provar que o pacote entregue é autocontido no rompath real do cliente. Sem inventar a causa da imagem.
4. **NG-04 — demais coleções:** aplicar a mesma verificação a todos os cartuchos, incluindo Metal Slug/Samurai/Fatal Fury e outros nomes presentes; devolver relatório por item, não apenas total. Identificar arquivos de BIOS/pais faltantes ou divergentes.
5. **NG-05 — CD:** confirmar formatos e launchPaths ativos dos 50 discos; verificar integridade CHD e dependência de pai. Não fornecer firmware ou jogo por fontes não autorizadas. Explicar se o artefato inclui firmware autorizado ou se depende da importação local já implementada.
6. **NG-06 — classificação:** confirmar `pspikes`/`spy` e apresentar plano que preserve itemId, capas, descritores, jogos instalados e saves se uma mudança for necessária. Não mudar IDs para contornar o diagnóstico.
7. **NG-07 — entrega:** responder com alterações concretas, hashes/revisões antes/depois, testes de artefato real e limitações. Se corrigir um pacote, atualizar de forma coerente o descritor assinado, tamanhos, contagem, launchPath e revisão mantendo o contrato. Não declarar gameplay Android validado a partir de teste Linux.

Resposta esperada: `docs/station-android/RETORNO-AUDITORIA-NEOGEO-CARTUCHO-CD-R65-20261007.md`, citando este pedido e o commit exato do app acima. **Não publicar ROM/BIOS, chaves, tokens, dados pessoais ou logs brutos.** Inventários de nomes/tamanhos/hashes e erros sanitizados são suficientes.

Nenhuma implantação Linux foi realizada pelo agente Windows. Este pedido não cancela o trabalho de retomada online nem autoriza alterações nos outros produtos.
