// SPDX-License-Identifier: GPL-2.0-or-later
// Diagnostic frontend for the exact received ARM64 core. No app replacement.
#include "libretro.h"
#include "PsxPinnedOptions.h"
#include <cstdio>
#include <cstdlib>
#include <cstdarg>
#include <cstring>
#include <cstdint>
#include <dlfcn.h>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

static std::string work;
static uint16_t pads[2]{};
static uint64_t polls[2]{}, nonzero[2]{}, frames{}, videos{}, changed{}, sound{};
static uint64_t video_hash{};
static unsigned pixel=RETRO_PIXEL_FORMAT_0RGB1555,width{},height{};
static std::vector<uint8_t> rgb, raw_pixels;
static uint64_t video_samples{};
static retro_disk_control_ext_callback disks{};
static uint64_t hash_bytes(const void*ptr,size_t n,uint64_t h=1469598103934665603ULL){
  auto p=static_cast<const uint8_t*>(ptr);for(size_t i=0;i<n;i++){h^=p[i];h*=1099511628211ULL;}return h;
}
static void core_log(enum retro_log_level level,const char*fmt,...){
  (void)level;va_list args;va_start(args,fmt);vfprintf(stderr,fmt,args);va_end(args);
}
static bool environment(unsigned command,void*data){
  switch(command){
  case RETRO_ENVIRONMENT_GET_LOG_INTERFACE:static_cast<retro_log_callback*>(data)->log=core_log;return true;
  case RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY:
  case RETRO_ENVIRONMENT_GET_SAVE_DIRECTORY:
  case RETRO_ENVIRONMENT_GET_CORE_ASSETS_DIRECTORY:*static_cast<const char**>(data)=work.c_str();return true;
  case RETRO_ENVIRONMENT_GET_VARIABLE:{
    auto v=static_cast<retro_variable*>(data);v->value=station_psx::pinned_option(v->key);
    if(!v->value)fprintf(stderr,"UNPINNED_OPTION %s\n",v->key);return v->value!=nullptr;
  }
  case RETRO_ENVIRONMENT_GET_VARIABLE_UPDATE:*static_cast<bool*>(data)=false;return true;
  case RETRO_ENVIRONMENT_GET_CORE_OPTIONS_VERSION:*static_cast<unsigned*>(data)=2;return true;
  case RETRO_ENVIRONMENT_GET_INPUT_MAX_USERS:*static_cast<unsigned*>(data)=2;return true;
  case RETRO_ENVIRONMENT_GET_INPUT_BITMASKS:return false; // Exact online owner uses individual IDs0..13.
  case RETRO_ENVIRONMENT_GET_CAN_DUPE:*static_cast<bool*>(data)=true;return true;
  case RETRO_ENVIRONMENT_SET_PIXEL_FORMAT:pixel=*static_cast<unsigned*>(data);return pixel<=RETRO_PIXEL_FORMAT_RGB565;
  case RETRO_ENVIRONMENT_GET_LANGUAGE:*static_cast<unsigned*>(data)=RETRO_LANGUAGE_ENGLISH;return true;
  case RETRO_ENVIRONMENT_GET_AUDIO_VIDEO_ENABLE:*static_cast<int*>(data)=3;return true;
  case RETRO_ENVIRONMENT_GET_DISK_CONTROL_INTERFACE_VERSION:*static_cast<unsigned*>(data)=1;return true;
  case RETRO_ENVIRONMENT_SET_DISK_CONTROL_EXT_INTERFACE:disks=*static_cast<retro_disk_control_ext_callback*>(data);return true;
  case RETRO_ENVIRONMENT_SET_VARIABLES:
  case RETRO_ENVIRONMENT_SET_CORE_OPTIONS:
  case RETRO_ENVIRONMENT_SET_CORE_OPTIONS_INTL:
  case RETRO_ENVIRONMENT_SET_CORE_OPTIONS_V2:
  case RETRO_ENVIRONMENT_SET_CORE_OPTIONS_V2_INTL:
  case RETRO_ENVIRONMENT_SET_CORE_OPTIONS_DISPLAY:
  case RETRO_ENVIRONMENT_SET_CORE_OPTIONS_UPDATE_DISPLAY_CALLBACK:
  case RETRO_ENVIRONMENT_SET_INPUT_DESCRIPTORS:
  case RETRO_ENVIRONMENT_SET_CONTROLLER_INFO:
  case RETRO_ENVIRONMENT_SET_PERFORMANCE_LEVEL:
  case RETRO_ENVIRONMENT_SET_GEOMETRY:
  case RETRO_ENVIRONMENT_SET_SYSTEM_AV_INFO:
  case RETRO_ENVIRONMENT_SET_MEMORY_MAPS:
  case RETRO_ENVIRONMENT_SET_SERIALIZATION_QUIRKS:return true;
  default:return false;
  }
}
static void video(const void*data,unsigned w,unsigned h,size_t pitch){
  if(!data||data==RETRO_HW_FRAME_BUFFER_VALID||!w||!h||w>2048||h>1024)return;
  width=w;height=h;videos++;uint64_t hash=1469598103934665603ULL;
  unsigned bytes=pixel==RETRO_PIXEL_FORMAT_XRGB8888?4:2;
  raw_pixels.resize(size_t(w)*h*bytes);
  for(unsigned y=0;y<h;y++)std::memcpy(raw_pixels.data()+size_t(y)*w*bytes,static_cast<const uint8_t*>(data)+y*pitch,w*bytes);
  if(videos==1||videos%30==0){
    hash=hash_bytes(raw_pixels.data(),raw_pixels.size());video_samples++;
    if(hash!=video_hash)changed++;video_hash=hash;
  }
}
static void audio(int16_t a,int16_t b){if(a||b)sound++;}
static size_t audio_batch(const int16_t*p,size_t n){for(size_t i=0;i<n*2;i++)if(p[i])sound++;return n;}
static void poll(){}
static int16_t input(unsigned port,unsigned device,unsigned index,unsigned id){
  (void)index;if(port>1||device!=RETRO_DEVICE_JOYPAD)return 0;
  polls[port]++;
  auto value=id==RETRO_DEVICE_ID_JOYPAD_MASK?pads[port]:(id<16?uint16_t((pads[port]>>id)&1):0);
  if(value)nonzero[port]++;return int16_t(value);
}
template<class F>static F symbol(void*core,const char*name){
  auto fn=reinterpret_cast<F>(dlsym(core,name));if(!fn){fprintf(stderr,"MISSING_SYMBOL %s\n",name);exit(3);}return fn;
}
int main(int argc,char**argv){
  if(argc!=4)return 2;work=argv[3];
  void*core=dlopen(argv[1],RTLD_NOW|RTLD_LOCAL);if(!core){fprintf(stderr,"DLOPEN %s\n",dlerror());return 3;}
#define BIND(name) auto name##_fn=symbol<decltype(&name)>(core,#name)
  BIND(retro_set_environment);BIND(retro_set_video_refresh);BIND(retro_set_audio_sample);
  BIND(retro_set_audio_sample_batch);BIND(retro_set_input_poll);BIND(retro_set_input_state);
  BIND(retro_init);BIND(retro_deinit);BIND(retro_load_game);BIND(retro_unload_game);
  BIND(retro_run);BIND(retro_serialize_size);BIND(retro_serialize);BIND(retro_unserialize);
  BIND(retro_get_memory_data);BIND(retro_get_memory_size);BIND(retro_get_region);BIND(retro_get_system_info);
  BIND(retro_set_controller_port_device);
  retro_set_environment_fn(environment);retro_set_video_refresh_fn(video);
  retro_set_audio_sample_fn(audio);retro_set_audio_sample_batch_fn(audio_batch);
  retro_set_input_poll_fn(poll);retro_set_input_state_fn(input);retro_init_fn();
  retro_set_controller_port_device_fn(0,RETRO_DEVICE_JOYPAD);
  retro_set_controller_port_device_fn(1,RETRO_DEVICE_JOYPAD);
  retro_game_info game{argv[2],nullptr,0,nullptr};
  bool loaded=retro_load_game_fn(&game);
  if(!loaded){std::cout<<"{\"loaded\":false}"<<std::endl;return 4;}
  retro_system_info info{};retro_get_system_info_fn(&info);
  std::cout<<"{\"loaded\":true,\"stateBytes\":"<<retro_serialize_size_fn()
    <<",\"region\":"<<retro_get_region_fn()<<",\"discs\":"<<(disks.get_num_images?disks.get_num_images():0)<<"}"<<std::endl;
  std::string line;
  while(std::getline(std::cin,line)){
    std::istringstream cmd(line);std::string action;cmd>>action;
    if(action=="quit")break;
    if(action=="step"){
      unsigned count{},p0{},p1{};cmd>>count>>p0>>p1;if(count>10000||p0>0x3fff||p1>0x3fff)return 5;
      pads[0]=p0;pads[1]=p1;
      for(unsigned i=0;i<count;i++){retro_run_fn();frames++;}
    }else if(action=="save"||action=="load"){
      std::string file;cmd>>file;size_t n=retro_serialize_size_fn();bool ok=false;
      if(n>0&&n<=16*1024*1024){std::vector<uint8_t> state(n);
        if(action=="save"){
          ok=retro_serialize_fn(state.data(),state.size());if(ok){std::ofstream f(file,std::ios::binary);f.write(reinterpret_cast<char*>(state.data()),state.size());ok=bool(f);}
        }else{
          std::ifstream f(file,std::ios::binary);f.read(reinterpret_cast<char*>(state.data()),state.size());ok=f.gcount()==static_cast<std::streamsize>(n)&&retro_unserialize_fn(state.data(),n);
        }
      }
      std::cout<<"{\"stateOperation\":\""<<action<<"\",\"passed\":"<<(ok?"true":"false")<<",\"stateBytes\":"<<n<<"}"<<std::endl;continue;
    }else if(action=="memory"){
      std::string file;cmd>>file;
      auto p=retro_get_memory_data_fn(RETRO_MEMORY_SYSTEM_RAM);auto n=retro_get_memory_size_fn(RETRO_MEMORY_SYSTEM_RAM);
      if(!p||n!=2097152)return 6;
      std::ofstream f(file,std::ios::binary);f.write(reinterpret_cast<const char*>(p),n);
    }else if(action=="image"){
      std::string file;cmd>>file;rgb.resize(size_t(width)*height*3);
      for(size_t i=0;i<size_t(width)*height;i++){
        uint32_t c=pixel==RETRO_PIXEL_FORMAT_XRGB8888?reinterpret_cast<const uint32_t*>(raw_pixels.data())[i]:reinterpret_cast<const uint16_t*>(raw_pixels.data())[i];
        auto out=rgb.data()+i*3;
        if(pixel==RETRO_PIXEL_FORMAT_XRGB8888){out[0]=(c>>16)&255;out[1]=(c>>8)&255;out[2]=c&255;}
        else if(pixel==RETRO_PIXEL_FORMAT_RGB565){out[0]=((c>>11)&31)*255/31;out[1]=((c>>5)&63)*255/63;out[2]=(c&31)*255/31;}
        else{out[0]=((c>>10)&31)*255/31;out[1]=((c>>5)&31)*255/31;out[2]=(c&31)*255/31;}
      }
      std::ofstream f(file,std::ios::binary);f<<"P6\n"<<width<<" "<<height<<"\n255\n";f.write(reinterpret_cast<char*>(rgb.data()),rgb.size());
    }
    if(!raw_pixels.empty())video_hash=hash_bytes(raw_pixels.data(),raw_pixels.size());
    auto memory=retro_get_memory_data_fn(RETRO_MEMORY_SYSTEM_RAM);size_t n=retro_get_memory_size_fn(RETRO_MEMORY_SYSTEM_RAM);
    std::cout<<"{\"frames\":"<<frames<<",\"videoFrames\":"<<videos<<",\"videoHash\":"<<video_hash
      <<",\"changedVideoSamples\":"<<changed<<",\"nonzeroAudioSamples\":"<<sound
      <<",\"ramHash\":"<<(memory?hash_bytes(memory,n):0)<<",\"ramBytes\":"<<n
      <<",\"portPolls\":["<<polls[0]<<","<<polls[1]<<"],\"nonzeroPortPolls\":["<<nonzero[0]<<","<<nonzero[1]
      <<"],\"width\":"<<width<<",\"height\":"<<height<<"}"<<std::endl;
  }
  retro_unload_game_fn();retro_deinit_fn();return 0;
}
