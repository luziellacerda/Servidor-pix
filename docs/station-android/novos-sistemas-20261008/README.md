# Pacote único: novos sistemas Station — 08/10/2026

Leia primeiro [o handoff](../RETORNO-NOVOS-SISTEMAS-JOGOS-CAPAS-STATION-20261008.md). **Aplicado:** revisão24, 114 novos jogos/capas, 3.848 IDs e 3.593 visíveis; 255 de compatibilidade.

| Arquivo | Conteúdo |
|---|---|
| `summary.json` | Contagens completas, plataformas e associação das capas |
| `catalog-completo-rev24.json`, `catalog-completo.tsv` | Todos os 3.848 IDs, nomes, capas, metadados e descritores sanitizados |
| `novos-jogos-capas-artefatos.json`, `novos-jogos.tsv` | Os 114 novos jogos, ROM relativa, capa original/compilada, origem/hash e arquivo de lançamento |
| `production-proof.json`, `http-proof.json` | Ativação efetiva, catálogo assinado, todas as capas e cinco streams representativos |
| `tested-tools.json` | Hashes do importador/dependências e 30 testes aprovados |
| `folder-organization.json` | Recorte/colagem de cinco pastas, preservação de 165 arquivos |
| `wiiu-local-completion.json` | Restauração local dos 3.009 arquivos ausentes de Mario Kart8 |
| `covers-installed.json`, `four-cover-sources.json` | Imagens instaladas e fontes verificadas das quatro complementares |
| `sinopses-ainda-ausentes.json` | Quatro jogos novos sem descrição e todas as lacunas anteriores |
| `metadata/` | Os cinco gamelists e índices de 289 chaves já instalados no HD |
| `delivery-manifest.json` | Tamanho e SHA256 de cada arquivo desta pasta |
| `build_delivery.py` | Geração a partir dos recibos privados de uma implantação concluída |
| `check_bundle.py` | Conferência independente de hashes, IDs, contagens e correspondências |

As ROMs e os arquivos de configuração privados não fazem parte da entrega Git. As capas e os jogos são obtidos pelo app na API autenticada. O JSON de inventário não é um envelope assinado do servidor.

Conferir o pacote após recebê-lo:

```sh
python3 check_bundle.py
```

O importador e os operadores estão no `scripts/` do diretório pai. Os operadores de movimentação/ativação são de primeira aplicação e já foram executados no Linux; não os executar no PC do APK nem repetir sobre a produção ativa.
