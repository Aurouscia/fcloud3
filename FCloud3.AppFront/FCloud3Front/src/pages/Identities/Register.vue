<script setup lang="ts">
import { Ref, onMounted, ref } from 'vue';
import { injectApi, injectPop } from '@/provides';
import { Api } from '@/utils/com/api';
import Pop from '@/components/Pop.vue';
import { useIdentityRoutesJump } from '@/pages/Identities/routes/routesJump';
import Notice from '@/components/Notice.vue';

const { jumpToLogin } = useIdentityRoutesJump();
const userName = ref<string>("");
const password = ref<string>("");
const passwordRepeat = ref<string>("");

async function register(){
    if(!userName.value){
        pop.value.show("用户名不能为空","failed");
        return;
    }
    if(!password.value || password.value.length <= 6){
        pop.value.show("密码过短", "failed");
        return;
    }
    if(password.value != passwordRepeat.value){
        pop.value.show("前后两次输入密码不一致", "failed");
        return;
    }
    const res = await api.identites.user.create(userName.value, password.value)
    if(res){
        jumpToLogin(false)
    }
}

let api:Api;
let pop:Ref<InstanceType<typeof Pop>>;
let applyWay = ref<string>("");
onMounted(async()=>{
    api = injectApi();
    pop = injectPop();
    applyWay.value = await api.etc.utils.applyBeingMember() || "暂不开放申请"
})
</script>

<template>
    <div>
        <h1>注册</h1>
    </div>
    <div>
        <table>
            <tr>
                <td>昵称</td>
                <td>
                    <input v-model="userName" type="text"/>
                </td>
            </tr>
            <tr>
                <td>密码</td>
                <td>
                    <input v-model="password" type="password"/>
                </td>
            </tr>
            <tr>
                <td>再次输入密码</td>
                <td>
                    <input v-model="passwordRepeat" type="password"/>
                </td>
            </tr>
        </table>
        <div class="reg">
            <button @click="register" class="confirm">注&nbsp;册</button>
        </div>
        <div class="notice">
            <Notice :type="'info'" :max-width="'300px'">
                为了确保内容合法合规，本平台为邀请制，新注册的账号为“游客”，不能进行编辑性质操作。
                编辑内容需要“正式成员”身份。<br/><br/>
                {{ applyWay }}
            </Notice>
        </div>
    </div>
</template>

<style scoped>
table{
    margin:auto;
    background-color: transparent;
    font-size: large;
    color:gray
}
td{
    background-color: transparent;
}
input{
    background-color: #eee;
}
.reg{
    display: flex;
    justify-content: center;
}
.notice{
    margin-top: 100px;
}
</style>