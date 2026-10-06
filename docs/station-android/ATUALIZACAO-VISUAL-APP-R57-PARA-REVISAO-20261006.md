# APP → SERVIDOR: sucessora visual R57 instalada

Complemento ao `PEDIDO-ANALISE-APP-R55-STATION-20261006.md`, publicado em `5722e50b19a19202bc01051277efb5fb7d712b89`. **Não é retorno do servidor, mudança de contrato nem integração do candidato de prontidão.**

Enquanto o pedido R55 era entregue, o mantenedor solicitou dois ajustes visuais. Foram concluídos, compilados e instalados no Motorola Edge30, preservando dados/assinatura. O APK instalado foi conferido por SHA-256 integral e abriu ESActivity sem pedir login.

## Identidade atual

- Fonte app: branch `review/station-r57-layout-20261006`, commit completo **`8980cd422d63068299b5e9946c120f81a9c94f29`**. A linha `feat/station-capas-visuais-netplay-20261003` também aponta para esse commit.
- [README e mapa da fonte](https://github.com/luziellacerda/TurboElden/blob/8980cd422d63068299b5e9946c120f81a9c94f29/versions/station-layout-r57-20261006/README.md).
- [Estado/instalação](https://github.com/luziellacerda/TurboElden/blob/8980cd422d63068299b5e9946c120f81a9c94f29/versions/station-layout-r57-20261006/STATUS.json).
- [Manifesto do overlay](https://github.com/luziellacerda/TurboElden/blob/8980cd422d63068299b5e9946c120f81a9c94f29/versions/station-layout-r57-20261006/SOURCE-MANIFEST.json).
- APK R57: `e6159fa3564f0b30c1415b062f42e08e47ea625832e1b2640375b3e8b2b55566`, 2.093.276.800bytes.
- DEX35: `cf3a92bce2927c0561c64783ae0030c6e5dc90fa81264564d977974a746c74fd`.
- Nativo carrossel: `1dd67942358abebca8e82a4c457a9d4163a48b2e73aa856ce62ab78018cde922`.

## Composição exata do código atual

1. Snapshot completo de código R55: `versions/station-current-r55-20261006`, commit `9d3d45f048aa44bb2ee9c41f567e985901628daa`.
2. Aplicar sobre ele apenas os arquivos homônimos e adicionais do overlay `versions/station-layout-r57-20261006` neste commit. Os demais arquivos permanecem R55.
3. Foram conferidos os157 hashes de entrada Java da compilação contra essa composição. Nenhum corpo vazio/substituto foi criado para fechar build.

## Alterações visuais — preservar ao conciliar sua revisão

- `StationRoomsActivity` recebeu apresentação compacta de **Criar sala**, com botões simétricos de largura limitada e `StationCreateGameCard` novo exibindo a capa inteira ao lado. Usa o bitmap/capa do cache existente, sem download adicional. O descarte do bitmap limpa as duas referências. A escolha do jogo, criação, protocolo de sala e transporte mantêm as regras existentes.
- A barra preta superior do carrossel passou de10,5% para8,5% da altura da tela. Seus controles/perfil foram reposicionados verticalmente. **Capas e faixa INSTALADO foram preservadas byte a byte**, inclusive geometria e arte vermelha. O pedido de mover capas foi retirado pelo mantenedor; não reaplicar essa tentativa intermediária.

O relatório R55-01 a R55-08 continua necessário e válido. `StationSessionChannel`, `StationGameSession`, `StationRetroActivity` e `StationRelayTunnel` não mudaram nesta sucessora. O candidato de prontidão d1b535c **continua fora do APK**. Conciliar os três arquivos do candidato mantendo o canal normalizado, a saída idempotente e o HUD; ao tocar `StationRoomsActivity`, usar a edição R57 deste overlay.

## Evidências e limites

Java8/API34/D8min26, compilação nativa,8.820 casos de geometria, assinatura/alinhamento16KiB e preservação integral das entradas não alteradas passaram. Instalação/hash e retorno ao catálogo com sessão foram observados no aparelho. A barra fina foi capturada na tela de plataformas. A conferência visual de Criar sala continua pendente: a captura após a resposta do usuário ainda mostrava plataformas.

Um teste Android isolado de layout foi encerrado pelo aparelho antes de produzir resultado; não contabilizar como aprovado. Não há nova comprovação de partida entre dois aparelhos, retorno completo da emulação online, controles próprios online ou redução térmica. Não implantar servidor nem outro APK por consequência deste adendo.

**Ao responder ao pedido R55, citar também que este adendo R57 foi considerado.** Isso evita outra proposta sobre uma Activity de salas já ultrapassada, sem confundir ajustes de layout com a correção funcional da partida.
