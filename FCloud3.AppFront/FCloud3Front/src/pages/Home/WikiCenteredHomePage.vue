<script setup lang="ts">
import Loading from '@/components/Loading.vue';
import { WikiCenteredHomePage } from '@/models/etc/wikiCenteredHomePage';
import { injectApi } from '@/provides';
import { onMounted, ref } from 'vue';
import { useWikiParsingRoutesJump } from '@/pages/WikiParsing/routes/routesJump';
import { useFilesRoutesJump } from '../Files/routes/routesJump';
import { useFeVersionChecker } from '@/utils/feVersionCheck';

const api = injectApi();
const model = ref<WikiCenteredHomePage>();
const { jumpToViewWiki } = useWikiParsingRoutesJump();
const { jumpToRootDir } = useFilesRoutesJump();
const { checkAndPop } = useFeVersionChecker();
async function init(){
    const resp = await api.etc.wikiCenteredHomePage.get();
    if(resp){
        model.value = resp;
    }
}
onMounted(async()=>{
    await init();
    checkAndPop();
})
</script>

<template>
<div class="wchp" v-if="model">
    <div class="upper">
        <div class="list">
            <div class="listTitle">
                最近更新
            </div>
            <div v-for="w in model.LatestWikis" :key="w.Path" @click="jumpToViewWiki(w.Path)" class="listItem">
                {{ w.Title }}
            </div>
        </div>
        <div class="list">
            <div class="listTitle">
                随机看看
            </div>
            <div v-for="w in model.RandomWikis" :key="w.Path" @click="jumpToViewWiki(w.Path)" class="listItem">
                {{ w.Title }}
            </div>
        </div>
    </div>    
    <div class="lower">
        <div class="list">
            <div class="twinListRow">
                <div class="listTitle">
                    根文件夹
                </div>
                <div class="listTitle">
                    其最新更新
                </div>
            </div>
            <div v-for="p in model.TopDirs" class="twinListRow">
                <div class="listItem" @click="jumpToRootDir(p.DPath)">{{ p.DName }}</div>
                <div class="listItem" @click="jumpToViewWiki(p.WPath)">{{ p.WTitle }}</div>
            </div>
        </div>
    </div>
</div>
<Loading v-else></Loading>
</template>

<style scoped lang="scss">
.upper{
    display: flex;
    justify-content: space-between;
    .list{
        width: 50%
    }
}
.twinListRow{
    display: flex;
    div{
        width: 50%;
        padding: 7px;
    }
}
.list{
    display: flex;
    flex-direction: column;
    .listItem,.listTitle{
        font-weight: bold;
        border-bottom: 1px solid #ccc;
        cursor: pointer;
        transition: 0.5s;
        padding: 7px;
        &:hover{
            font-weight: bold;
            text-decoration: underline;
            background-color: #eee;
        }
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    .listTitle{
        color: #aaa;
        font-size: 18px;
        cursor: default;
        &:hover{
            text-decoration: none;
        }
    }
}
.wchp{
    display: flex;
    flex-direction: column;
    gap: 20px;
}
</style>