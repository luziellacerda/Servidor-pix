# Evidência consolidada R81

Leia primeiro [o retorno de execução](../RETORNO-EXECUCAO-HANDOFF-R81-20261008.md). Dados públicos de integração e ensaios sintéticos; nenhum segredo, ROM ou caminho privado de conteúdo.

- `BASE-SERVIDOR-APLICADA.json`: DLL/PID/horário reais, sombras antiga/nova/retorno, catálogo e online públicos, isolamento e limites. v3/gate continuam desligados; zero perfis aprovados.
- `IDENTIDADES-APLICADAS.json` e `QUALIFICACAO-IDENTIDADES.json`: preparação real offline, publicação no índice20 e duas varreduras idênticas.
- `CATALOGO-REV20.json`: 3.734 IDs sanitizados, nomes/plataformas/coverId/metadados/descritores/contentSha256; 3.479 visíveis e 255 compatíveis ocultos.
- `CONTENT-IDENTITIES.json`: 2.071 vínculos de payload/recipiente/lançamento, sem mapa de caminhos absolutos.
- `PERFIS-PILOTOS-NAO-APROVADOS.json`: três vínculos exatos dos pilotos R81. Bomberman Battle/Single Match continua proposto para2/3/4, sem homologação.
- `PERFIS-R76-E-PILOTOS-NAO-APROVADOS.json`: 2.071 rascunhos R76 mais três pilotos. **Não usar para ligar o gate global:** nenhum registro está aprovado; capacidades e modos precisam de qualificação física. Compatibilidade oculta não implica novas salas.
- `CONTINUIDADE-LEGADO.json`: cobertura factual/proposta por engine; evidencia o impedimento à ativação global.
- `QUALIFICACAO-TLS-V2.json`: resultados dos dois clientes e dos modos do fixture; conserva a falha intermitente do controle Python síncrono e limita a conclusão.
- `ENTREGA-CONFERIDA.json` e `SELO-BASE-R81.json`: 18 arquivos recebidos, 209 fontes Java, 19 fontes servidor, sete hashes canônicos e 12 arquivos de release.

Ferramentas auditáveis no repositório Servidor-pix em `../scripts/implantar-base-station-r81-20261008.py`, `verificar-catalogo-identidades-r81.py`, `station_async_socket_check.py` e `../../../tests/StationRecovery/qualify_tls.py`. A implantação desta fase usa autenticação Linux nativa, arquivos privados novos, CAS e restart somente vazio. O operador recusa baselines diferentes da revisão20/ab192bf inicial; não reaplicá-lo à release já ativa como se fosse primeira implantação.
