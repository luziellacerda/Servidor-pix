# Neo Geo CD — preparação de 05/10/2026

Pedido vigente: organizar a cópia em `neogeo/neogeocd` e aplicar o mesmo processo de jogos/capas/metadados/downloads automáticos. O usuário informou que não possui BIOS CD. A autorização local/global e o pedido de integração cobrem esta implantação isolada; manter a API e os demais serviços em execução.

## Dados e execução

- 50 arquivos `.img` são CHD v5, verificados integralmente com `chdman verify` 0.264 sem `--fix`. 50 revistas exatas 1024×1536 e 50 sinopses XML.
- Entrega sem BIOS: bytes idênticos, formato `raw`, `fileName=launchPath=<nome>.chd`, `fileCount=1`. Originais `.img`, XML, vídeos, saves e pastas permanecem intactos.
- BIOS disponível futuramente: somente firmware/zoom de identidades exatas; pacote ZIP_STORED externo com CHD e `neocdz.zip` fechado. Firmware nunca é cadastrado como jogo. A BIOS MVS é diferente da BIOS CD.
- O scanner registra `runtimeRequirements.neogeocd.biosAvailable=false` enquanto faltar BIOS. A falta não esconde discos válidos nem bloqueia download. A ponte Android deve permitir importar a BIOS e explicar a ausência ao abrir o jogo.
- A raiz filha configurada fica excluída da enumeração da plataforma pai; fingerprints Neo Geo existentes são mantidos. IDs, capas, downloads e metadados antigos devem permanecer byte idênticos.

## Alvo e retorno

Alvo: somente `turborama-station-library-scan.service`, sua configuração privada e o índice já utilizado pela API. Fonte limpa/commit exato; quatro módulos e verificador privado selados. Nenhuma instalação de pacote global, mudança Nginx/rede, migração, chave real ou reinício API.

`implantar-neogeocd-station-20261005.py --apply --source-revision <commit>` exige índice9/SHA revisado, 50 discos e recibo SHA completo. O recibo de verificação integral é reutilizado somente durante a importação inicial, após SHA256 completo e informação CD coincidirem. O mapa não é gravado na configuração ativa; novos arquivos recebem verificação integral.

Preview isolado: 50 novos itens, 50 revistas, 50 sinopses; 2.212 visíveis/255 compatíveis; todas as linhas anteriores exatamente iguais. Validação do leitor efetivo sob UID/GID da API, catálogo assinado HTTPS, uma amostra por plataforma, 50 capas com quatro trabalhadores, SHA e extensão do CHD, uso único do grant e limpeza sintética. Não comprova gameplay Android.

Backup novo: `/mnt/DADOS/station-neogeocd-backup-20261005`. Conteúdo novo: `/mnt/DADOS/turbostation-neogeocd-content-20261005`. Retorno: o mesmo script com `--rollback`, revisão monotônica superior; recusa apagar imports posteriores. Conteúdos/HD e API permanecem disponíveis.

## Fontes técnicas

[CHD/MAME](https://docs.mamedev.org/tools/chdman.html), [driver CDZ e identidades](https://github.com/mamedev/mame/blob/master/src/mame/snk/neogeocd.cpp), [intent MAME4droid](https://github.com/seleuco/MAME4droid-Current/blob/main/android-MAME4droid/app/src/main/java/com/seleuco/mame4droid/Emulator.java). A Universe BIOS oficial estabelece uso pessoal e proíbe redistribuição; não foi baixada/embutida neste conjunto. [Condições oficiais](http://unibios.free.fr/download.html).
