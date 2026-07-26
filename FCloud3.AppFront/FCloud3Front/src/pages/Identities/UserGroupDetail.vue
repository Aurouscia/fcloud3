<script setup lang="ts">
import { inject, onMounted, ref, useTemplateRef, watch } from 'vue';
import { Api } from '@/utils/com/api';
import { UserGroup, UserGroupDetailResult } from '@/models/identities/userGroup';
import Loading from '@/components/Loading.vue';
import Search from '@/components/Search.vue';
import { useIdentityRoutesJump } from './routes/routesJump';
import LongPress from '@/components/LongPress.vue';

const props = defineProps<{
    id:number
}>()

const {jumpToUserCenter} = useIdentityRoutesJump();

const data = ref<UserGroupDetailResult>();
const meShowItsLabel = ref(false);
const info = ref<UserGroup>();
async function loadData(){
    if(props.id==0){return;}
    var timer = setTimeout(()=>{
        data.value = undefined;
    },500);
    var res = await api.identites.userGroup.getDetail(props.id);
    if(res){
        info.value = {
            Id:res.Id,
            Name:res.Name
        }
        data.value = res;
        meShowItsLabel.value = res.MeShowItsLabel;
        clearTimeout(timer);
    }
}
const inviteUserSearch = useTemplateRef('inviteUserSearch')
async function inviteUser(_name:string, userId:number){
    var res = await api.identites.userGroup.addUserToGroup(userId,props.id)
    if(res){
        inviteUserSearch.value?.clear();
        loadData();
        emit('needRefresh');
    }
}
async function leaveGroup() {
    var res = await api.identites.userGroup.leave(props.id);
    if(res){
        loadData();
        emit('needRefresh');
    }
}
async function dissolveGroup() {
    var res = await api.identites.userGroup.dissolve(props.id);
    if(res){
        emit('dissolved');
    }
}
async function editInfo() {
    if(!info.value){return;}
    var res = await api.identites.userGroup.editExe(info.value)
    if(res){
        loadData();
        emit('needRefresh');
    }
}
async function removeUser(userId:number) {
    const res = await api.identites.userGroup.removeUserFromGroup(userId, props.id);
    if(res){
        loadData();
        emit('needRefresh');
    }
}
async function setShowLabel(e:MouseEvent) {
    e.preventDefault();
    const to = !meShowItsLabel.value;
    const res = await api.identites.userGroup.setShowLabel(props.id, to)
    if(!data.value)return;
    if(res){
        meShowItsLabel.value = to;
        data.value.MeShowItsLabel = to;
    }
}

const emit = defineEmits<{
    (e:'needRefresh'):void
    (e:'dissolved'):void
}>()
defineExpose({loadData})

var api:Api
onMounted(async()=>{
    api = inject('api') as Api;
    loadData();
})
watch(props,async ()=>{
    await loadData();
})
</script>

<template>
    <div v-if="props.id!==0" class="userGroupDetail">
        <div v-if="data" class="items">
            <div class="info">
                <div class="name">{{ data.Name }}</div>
                <div>组长: <RouterLink :to="`/u/${data.Owner}`">{{ data.Owner }}</RouterLink></div>
                <div>人数: {{ data.FormalMembers.length }}</div>
                <div v-if="data.IsMember" class="showLabel">
                    <input type="checkbox" v-model="meShowItsLabel" @click="setShowLabel"> 显示本组名称为头衔
                </div>
            </div>
            <div v-if="data.CanInvite" class="search">
                <Search ref="inviteUserSearch" :source="api.etc.quickSearch.userName" :allow-free-input="false" 
                    :no-result-notice="'未找到该用户'" :placeholder="'邀请用户加入'" @done="inviteUser"></Search>
            </div>
            <table><tbody>
                <tr v-if="data.Inviting && data.Inviting.length>0">
                    <td class="typeHead" :colspan="data.CanEdit?2:1">已邀请用户</td>
                </tr>
                <tr v-for="i in data.Inviting">
                    <td class="memberName" @click="jumpToUserCenter(i.Name)">{{ i.Name }}</td>
                    <td v-if="data.CanEdit"></td>
                </tr>
                <tr>
                    <td class="typeHead" :colspan="data.CanEdit?2:1">成员列表</td>
                </tr>
                <tr v-for="m in data.FormalMembers">
                    <td class="memberName" @click="jumpToUserCenter(m.Name)">{{ m.Name }}</td>
                    <td v-if="data.CanEdit" class="toMemberOpTd">
                        <button class="lite" @click="removeUser(m.Id)">移出</button>
                    </td> 
                </tr>
            </tbody></table>
            <table v-if="info && data.CanEdit">
            <thead>
                <tr><th colspan="2">编辑信息</th></tr>
            </thead>
            <tbody>
                <tr>
                    <td>名称</td>
                    <td><input v-model="info.Name"/></td>
                </tr>
                <tr>
                    <td colspan="2"><button @click="editInfo">保存</button></td>
                </tr>
            </tbody></table>
            <LongPress v-if="data.CanEdit" :reached="dissolveGroup" class="cancel">长按解散本组</LongPress>
            <LongPress v-if="data.IsMember" :reached="leaveGroup" class="cancel">长按退出本组</LongPress>
            <div v-else class="joinHint">要加入用户组，请联系组长邀请你</div>
        </div>
        <Loading v-else></Loading>
    </div>
    <div v-else class="userGroupDetail" style="color:#999;text-align: center;">
        请点击用户组名称查看详情
    </div>
</template>

<style scoped>
.joinHint{
    text-align: center;
    font-size: 14px;
    color: #666;
}
.toMemberOpTd{
    width: 50px;
}
.showLabel{
    color: #666;
    display: flex;
    align-items: center;
    justify-content: center;
}
a{
    color:black
}
.memberName{
    width: 200px;
    cursor: pointer;
    word-break: break-all;
}
.memberName:hover{
    background-color: #ccc;
    text-decoration: underline;
}
.memberName a{
    color:black;
}
.typeHead{
    background-color: #999;
    color:white;
}
.userGroupDetail{
    flex-basis: 250px;
    flex-grow: 1;
}
.items{
    display: flex;
    flex-direction: column;
    gap:10px;
    align-items: stretch;
}
.ops{
    display: flex;
    flex-direction: column;
    align-items: center;
}
.info{
    background-color: #eee;
    color:black;
    padding: 5px;
    border-radius: 5px;
}
.info>div{
    margin-bottom: 5px;
    margin-left: 10px;
}
.name{
    font-size: 20px;
    font-weight: bold;
}
</style>