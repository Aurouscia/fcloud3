import { inject, ref } from "vue";
import { Api } from "./com/api";

export function useUrlPathNameConverter(){
    const name = ref<string>();
    const converted = ref<string>();
    const api = inject('api') as Api;
    const run = async()=>{
        if(!name.value){return;}
        const res = await api.etc.utils.urlPathName(name.value);
        if(res){
            converted.value = res;
        }
    }
    return{name,converted,run}
}