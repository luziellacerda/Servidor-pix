# Contagem exibida no carrossel — R77

## Origem anterior e correção

O carrossel R75/R76 (`libturbo_carousel.so`, SHA256 `3c2e22bc94f3432280e2b08c2f26ab33508cd1278ee9bd750b48d45c35105a8b`) não recebia `StationCatalog.Item.players`. `StationFrontend.publishCurrent` publica oito campos; nenhum deles é jogadores. A tela consultava `StationGameDetails` compilado, com alternativa por plataforma/título. O formatador também convertia `1-4` para `4`.

A R77 substitui **somente a origem do contador de jogadores**. Na mudança da seleção, o carrossel chama `StationCatalogPlayerEvidence.labelFor(itemId)`. A consulta usa um mapa imutável em memória, preparado no executor do catálogo; não faz HTTP, lê arquivos, calcula hashes ou pesquisa nomes durante a renderização. O estilo aparece junto ao número: `4 sim.`, `2 alt.`, `1` ou `—`. Uma capacidade alternada nunca é mostrada como simultânea.

As estrelas continuam usando sua origem anterior. Geometria, mídia, proporções, cantos, animações, limite de quadros e emuladores não são alterados por este delta.

## Vínculo factual obrigatório

O APK contém o asset pequeno `assets/station-catalog/player-evidence-v1.json`. Cada registro futuro precisa conter:

```json
{
  "itemId": "<identificador exato do catálogo>",
  "itemRevision": 4,
  "contentSha256": "<SHA256 minúsculo do conteúdo exato, 64 hexadecimais>",
  "evidenceStatus": "verified-exact-content",
  "modes": [
    {
      "modeId": "<modo documentado e revisado>",
      "playStyle": "simultaneous",
      "allowedHumanCounts": [2, 3, 4],
      "sourceRefs": ["<referência documental revisada>"]
    }
  ]
}
```

O catálogo autenticado precisa publicar também `items[].contentSha256`. O cliente preserva esse campo opcional no cache. O contador só aparece quando **itemId, revisão do item e contentSha256** coincidem exatamente com o registro revisado. Campo ausente/malformado, conteúdo diferente, revisão diferente, modo desconhecido, contagem inválida ou evidência de pesquisa produzem `—`. Não há herança entre original/tradução/hack, edições ou títulos parecidos.

O catálogo nativo aplica a publicação de forma assíncrona e não carrega o hash por célula. Para impedir que uma célula antiga consulte o registro de uma edição nova, o helper memoriza os vínculos observados: qualquer mudança de revisão ou hash para o mesmo ID mantém esse ID em `—` até reiniciar o processo. Remover/reinserir o item ou reverter a revisão não retira a invalidação. O histórico aceita no máximo 40.000 IDs; se esgotado, a apresentação continua desconhecida até reiniciar e a memória não cresce. IDs novos e vínculos intactos continuam elegíveis enquanto houver capacidade. Isso é uma guarda de apresentação, não bloqueia catálogo, download ou jogo.

`playStyle` admite somente `single-player`, `simultaneous` e `alternating`; a contagem é um conjunto estritamente crescente de 1 a 16. Individual exige somente `[1]`; simultâneo/alternado precisam de máximo maior que 1. Esse limite é da apresentação, **não uma capacidade prometida pelo transporte online**.

## Estado factual nesta entrega

O asset revisado tem **zero registros**. A pesquisa ampla em `catalog-game-metadata.json` contém candidatos descritivos e não comprova o conteúdo de cada edição. Ela permanece fora dos assets do APK. Os seis vínculos documentais de modos também não foram promovidos a aprovação exata por nome/manual genérico.

Assim, esta correção remove números apresentados como certos sem prova e deixa o caminho pronto para dados verificados. Não significa que todos os títulos já tenham contagem confirmada. Não exige hash de ROM na navegação e não acessa arquivos de jogos para produzir o contador.

## Separação do online

O helper é somente apresentação. Não cria vagas, muda controle, escolhe motor ou autoriza uma partida. Capacidade/vagas/mode da sala continuam vindo do perfil exato aprovado e do roster assinados, apresentados por `StationGamePlayerInfo`.

## Escopo da validação

`carousel/build_carousel.py` exige reprodução byte a byte da biblioteca R75 antes do delta, valida seus 65 fontes, sete objetos e includes externos. Compila testes isolados do rótulo e regressões de navegação, rotas, cantos e vídeo. Os testes Java exercitam o helper real. Recibos das execuções são produzidos separadamente; compilar/testar isoladamente não confirma a aparência no telefone nem qualifica gameplay.
