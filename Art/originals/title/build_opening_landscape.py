from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
from scipy.ndimage import map_coordinates
import numpy as np,math,subprocess,io,os,json,zipfile

out=Path('output/opening_landscape');out.mkdir(parents=True,exist_ok=True)
def png(im,p):
 b=io.BytesIO();im.save(b,format='PNG')
 with open(p,'wb') as f:f.write(b.getvalue());f.flush();os.fsync(f.fileno())
bg=Image.open('generated_images/exec-7623b0df-8695-45ad-bc21-4c4e7ff1c3bf.png').convert('RGBA').resize((1280,720),Image.Resampling.LANCZOS);png(bg,out/'background_static.png');base=np.array(bg)
leaf=Image.open('generated_images/exec-6e79a1b9-65b3-4ac7-aafe-27219e022476.png').convert('RGBA');leaf=leaf.crop(leaf.getbbox());png(leaf,out/'evergreen_branch.png')
cloud=Image.open('generated_images/exec-107886a8-3fd0-4318-ad98-d9c07bc43999.png').convert('RGBA');cloud=cloud.crop(cloud.getbbox());png(cloud,out/'cloud_band.png')
leaves=[];meta=[]
for j,(rootx,rooty,w,flip,phase) in enumerate([(-18,49,150,False,.1),(-12,170,175,False,1.1),(-24,301,180,False,2.4),(20,391,128,False,3.2),(1295,93,159,True,.7),(1292,233,183,True,1.8),(1300,354,164,True,3.1),(1289,474,146,True,4.2)]):
 h=round(w*leaf.height/leaf.width);sp=leaf.resize((w,h),Image.Resampling.LANCZOS)
 if flip:sp=sp.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
 aa=np.array(sp);aa[:,:,:3]=(aa[:,:,:3]*.52).astype('uint8');sp=Image.fromarray(aa)
 pad=14;can=Image.new('RGBA',(w+pad*2,h+pad*2));can.paste(sp,(pad,pad))
 a=np.array(can).astype(float);a[:,:,:3]*=a[:,:,3:4]/255
 yy,xx=np.mgrid[:can.height,:can.width].astype(float);u=np.clip(((w+pad-xx) if flip else (xx-pad))/w,0,1)
 pos=(rootx-w-pad if flip else rootx-pad,rooty-pad)
 leaves.append((a,xx,yy,u,pos,phase));meta.append({'anchor':[rootx,rooty],'width':w,'flip':flip,'phase':phase})
protect=Image.new('L',(1280,720));pd=ImageDraw.Draw(protect)
pd.rectangle((430,75,855,211),fill=255);pd.rectangle((486,272,789,613),fill=255);pd.rectangle((41,454,371,676),fill=255)
protected=np.array(protect)>0
mask=Image.new('L',(1280,720));md=ImageDraw.Draw(mask)
md.polygon([(837,405),(1112,399),(1128,446),(1022,454),(965,482),(854,530),(794,550),(809,516),(923,457),(918,421)],fill=255)
gold=(base[:,:,0]>135)&(base[:,:,1]>85)&(base[:,:,0]>base[:,:,2]*1.22)
water=(np.array(mask)>0)&gold&~protected;png(Image.fromarray((water*255).astype('uint8')),out/'river_reflection_mask.png')
ys,xs=np.where(water);rng=np.random.default_rng(93);idx=rng.choice(len(xs),size=min(38,len(xs)),replace=False);glints=[(int(xs[k]),int(ys[k]),float(rng.uniform(0,1))) for k in idx]
yy,xx=np.mgrid[390:550,790:1140];wm=water[390:550,790:1140]
cy,cx=np.mgrid[:170,:240];sunmask=np.exp(-(((cx-120)/73)**2+((cy-85)/49)**2)*1.6)
clouds=[]
for x,y,w,ph in [(-54,21,405,.1),(879,68,406,.61),(855,187,290,.36)]:
 sp=cloud.resize((w,round(w*cloud.height/cloud.width)),Image.Resampling.LANCZOS);clouds.append((sp,x,y,ph))
fireworks=[(2.,258,264,238,135,(255,224,153)),(7.3,272,272,306,164,(196,222,255))]
def render(t):
 t=t%12;p=2*math.pi*t/12;scene=bg.copy()
 for sp,x,y,ph in clouds:
  q=(t/12+ph)%1;fade=math.sin(math.pi*q)**.8
  im=sp.copy();im.putalpha(im.getchannel('A').point(lambda v:int(v*.25*fade)))
  scene.alpha_composite(im,(round(x+62*q),y))
 # Local sunlight breathes gently without changing the mountains or camera.
 a=np.zeros((170,240,4),dtype='uint8');a[:,:,:3]=[255,201,111];a[:,:,3]=(sunmask*(20+12*math.sin(p+.2))).astype('uint8');scene.alpha_composite(Image.fromarray(a),(948,224))
 shimmer=(.5+.5*np.sin(yy*.53+xx*.017-4*p))*(.75+.25*np.sin(yy*.19+2*p))
 a=np.zeros((160,350,4),dtype='uint8');a[:,:,:3]=[255,232,161];a[:,:,3]=(wm*shimmer*64).astype('uint8');scene.alpha_composite(Image.fromarray(a),(790,390))
 fx=Image.new('RGBA',(1280,720));d=ImageDraw.Draw(fx)
 for x,y,ph in glints:
  alpha=int(150*max(0,math.sin(4*p+ph*math.pi*2))**7)
  d.line((x-2,y,x+3,y),fill=(255,241,188,alpha),width=1)
 for start,sx,sy,ex,ey,color in fireworks:
  q=t-start
  if 0<=q<.85:
   k=q/.85;x=sx+(ex-sx)*k;y=sy+(ey-sy)*k
   d.line((x,y+9,x,y),fill=(*color,100),width=1);d.ellipse((x-1,y-1,x+1,y+1),fill=(*color,220))
  elif .85<=q<2.25:
   k=(q-.85)/1.4;r=32*(1-math.exp(-k*4));alpha=int(215*(1-k)**1.35)
   for j in range(22):
    angle=2*math.pi*j/22;rr=r*(.8+.2*math.sin(j*4)**2)
    x=ex+math.cos(angle)*rr;y=ey+math.sin(angle)*rr+k*k*13
    x0=ex+math.cos(angle)*(rr-3);y0=ey+math.sin(angle)*(rr-3)+k*k*13
    d.line((x0,y0,x,y),fill=(*color,alpha),width=1)
 scene=Image.alpha_composite(scene,fx)
 for a,xxl,yyl,u,pos,phase in leaves:
  bend=(4.8*math.sin(2*p+phase)+.9*math.sin(3*p+.7*phase))*u*u
  coords=[yyl-bend,xxl]
  b=np.stack([map_coordinates(a[:,:,c],coords,order=1,mode='constant',cval=0) for c in range(4)],-1);b[:,:,:3]/=np.maximum(b[:,:,3:4]/255,1e-6)
  scene.alpha_composite(Image.fromarray(np.clip(b,0,255).astype('uint8')),pos)
 arr=np.array(scene);arr[protected]=base[protected]
 return Image.fromarray(arr).convert('RGB')

json.dump({'name':'가챠 용사 오프닝 — 석양의 성','canvas':[1280,720],'duration_seconds':12,'fps':24,'leaf_layers':meta,'leaf_motion':'Branch attachment fixed; breeze bends leaf tips in two slow cycles per loop. Only separate RGBA foliage sprites are transformed.','cloud_motion':'62px slow drift per cloud lifetime; fade before wrap; staggered phases','sun_glow':'Local low-amplitude warm light','river':'Mask-limited traveling horizontal light ribbons and fine glints','fireworks':[{'start':a,'launch':[b,c],'burst':[d,e],'color':f} for a,b,c,d,e,f in fireworks],'fixed_regions':'Title, menu frames and labels, three summon cards, castle, terrain'},open(out/'layers.json','w'),ensure_ascii=False,indent=2)
mp=subprocess.Popen(['ffmpeg','-y','-v','error','-f','rawvideo','-pix_fmt','rgb24','-s','1280x720','-r','24','-i','-','-c:v','libx264','-threads','2','-crf','17','-pix_fmt','yuv420p','-movflags','+faststart',str(out/'opening_landscape_loop.mp4')],stdin=subprocess.PIPE)
small=[];checks=[]
for i in range(288):
 f=render(i/24);mp.stdin.write(f.tobytes())
 if i%2==0:small.append(f.resize((960,540),Image.Resampling.LANCZOS))
 if i in [0,72,200]:png(f,out/f'preview_{i:03d}.png');checks.append(np.array(f))
mp.stdin.close();assert mp.wait()==0
subprocess.run(['ffmpeg','-v','error','-i',str(out/'opening_landscape_loop.mp4'),'-f','null','-'],check=True)
gf=subprocess.Popen(['ffmpeg','-y','-v','error','-f','rawvideo','-pix_fmt','rgb24','-s','960x540','-r','12','-i','-','-filter_complex','split[a][b];[a]palettegen[p];[b][p]paletteuse=dither=none','-loop','0',str(out/'opening_landscape_loop.gif')],stdin=subprocess.PIPE)
for f in small:gf.stdin.write(f.tobytes())
gf.stdin.close();assert gf.wait()==0
im=Image.open(out/'opening_landscape_loop.gif')
for i in range(im.n_frames):im.seek(i);im.load()
assert np.array_equal(np.array(render(0)),np.array(render(12)))
for a in checks[1:]:assert np.array_equal(checks[0][protected],a[protected])
for p in out.glob('*.png'):Image.open(p).load()
print('Full MP4,',im.n_frames,'GIF frames and PNG assets verified. Fixed UI/cards, exact loop phase match, water mask pixels:',water.sum())
(out/'README.txt').write_text('가챠 용사 오프닝 배경 애니메이션\n12초 반복 / 1280x720 MP4 24fps / 960x540 GIF 12fps / 무음\n양쪽 나무에 별도 잎 레이어를 추가하여 가지를 고정하고 흔들었습니다.\n구름의 느린 이동, 국소적인 석양 빛, 강의 반사광, 성에서 올라가는 작은 폭죽 두 발을 합성했습니다.\n타이틀·메뉴·카드 3장은 고정. 배경 전체를 변형하지 않습니다.\n고정 배경, 투명 잎·구름, 강 반사 마스크, layers.json 및 재생 코드 포함.\n',encoding='utf-8')
with zipfile.ZipFile('output/opening_landscape_layered.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(out.iterdir()):z.write(p,p.name)
 z.write('build_opening_landscape.py','build_opening_landscape.py')
