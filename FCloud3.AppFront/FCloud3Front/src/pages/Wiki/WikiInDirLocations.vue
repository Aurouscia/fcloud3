<script setup lang="ts">
import { WikiInDirLocationView } from '@/models/wiki/wikiItem';
import { injectApi } from '@/provides';
import { onMounted, ref } from 'vue';
import Loading from '@/components/Loading.vue';
import { useFilesRoutesJump } from '../Files/routes/routesJump';
import { useWikiParsingRoutesJump } from '../WikiParsing/routes/routesJump';
import Search from '@/components/Search.vue';
import { useRouter } from 'vue-router';

const props = defineProps<{
    urlPathName:string
}>();

const { jumpToDirFromId } = useFilesRoutesJump();
const { jumpToViewWiki } = useWikiParsingRoutesJump();
const router = useRouter();

function done(){
    if(window.history.state?.back){
        router.back();
    }else{
        jumpToViewWiki(props.urlPathName);
    }
}

async function load() {
    data.value = await api.wiki.wikiItem.viewDirLocations(props.urlPathName)
}
async function removeFrom(dirId:number){
    if(!data.value)
        return;
    const resp = await api.wiki.wikiItem.removeFromDir(data.value.WikiId, dirId);
    if(resp){
        await load();
    }
}
async function addTo(dirId:number) {
    if(!data.value)
        return;
    const resp = await api.files.fileDir.putInThings(dirId,[],[],[data.value.WikiId]);
    if(resp){
        await load();
    }
}

const api = injectApi();
const data = ref<WikiInDirLocationView>();
onMounted(async()=>{
    await load();
})
</script>

<template>
    <h1>
        {{data?.Title}}-词条位置管理
        <button class="ok" @click="done()">完成</button>
    </h1>
    <div class="searching">
        <Search :source="api.etc.quickSearch.fileDir" @done="(_val,id)=>addTo(id)" :placeholder="'添加本词条到目录'"></Search>
    </div>
    <div v-if="data" class="list">
        <div v-for="loc in data.Locations" class="item">
            <div @click="jumpToDirFromId(loc.Id)">
                {{ loc.NameChain }}
            </div>
            <button class="lite" @click="removeFrom(loc.Id)">移出</button>
        </div>
        <div v-if="data.Locations.length==0" class="emptyNotice">
            本词条没有被收录于任何目录
        </div>
    </div>
    <Loading v-else></Loading>
</template>

<style scoped lang="scss">
.emptyNotice{
    text-align: center;
    color: #999
}
h1{
    display: flex;
    justify-content: space-between;
    button{
        font-size: medium;
        white-space: nowrap;
        flex-shrink: 1;
    }
}
.searching{
    margin: 10px;
    overflow: visible;
}
.list{
    display: flex;
    flex-direction: column;
    gap:2px;
}
.item{
    padding: 5px;
    background-color: #f8f8f8;
    display: flex;
    align-items: center;
    justify-content: space-between;
    div{
        height: 20px;
        line-height: 20px;
        cursor: pointer;
        &:hover{
            text-decoration: underline;
        }
    }
    &:hover{
        background-color: #ddd;
    }
}
</style>