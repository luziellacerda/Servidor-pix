#pragma once
// These five exact corrupted paths occur in the pinned revision-14 public TSV.
// R67's independent catalog fixture contains their correctly encoded forms.
// Resolve editorial text only: never rename folders, alter membership or media.
static inline const char*stationCollectionEditorialPath(const char*platform,const char*path){
 if(!platform||!path||!collectionKeyEqual(platform,"Neo Geo"))return path;
 struct Alias {const char*published;const char*editorial;};
 static const Alias aliases[]={
  {"# 0 - ART OF FIGTHERS COLE\ufffd\ufffdO #","# 0 - ART OF FIGTHERS COLE\u00c7\u00c3O #"},
  {"# 1 - METAL SLUG COLE\ufffd\ufffdO #","# 1 - METAL SLUG COLE\u00c7\u00c3O #"},
  {"# 2 - SAMURAI SHODOWN COLE\ufffd\ufffdO #","# 2 - SAMURAI SHODOWN COLE\u00c7\u00c3O #"},
  {"# 3 - THE KING OF FIGTHERS COLE\ufffd\ufffdO #","# 3 - THE KING OF FIGTHERS COLE\u00c7\u00c3O #"},
  {"# 4 - FATAL FURY COLE\ufffd\ufffdO #","# 4 - FATAL FURY COLE\u00c7\u00c3O #"}
 };
 for(const auto&alias:aliases)if(collectionKeyEqual(path,alias.published))return alias.editorial;
 return path;
}
